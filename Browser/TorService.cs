using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Kamui.Browser;

internal sealed class TorService : IDisposable
{
    private readonly Process _process;
    private readonly int _controlPort;
    private readonly string _authenticationCookiePath;
    private readonly string _dataDirectory;
    private string _exitIp = string.Empty;
    private bool _disposed;

    public string ProxyServer { get; }
    public string ExitIp => _exitIp;

    public TorService()
    {
        var torBinary = ResolveTorBinary();

        var socksPort = FindFreePort();
        _controlPort = FindFreePort();
        _dataDirectory = Path.Combine(
            Path.GetTempPath(),
            "kamui-tor",
            $"{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dataDirectory);
        _authenticationCookiePath = Path.Combine(_dataDirectory, "control_auth_cookie");
        ProxyServer = $"socks5://127.0.0.1:{socksPort}";

        using var bootstrapComplete = new ManualResetEventSlim();
        var errors = new List<string>();
        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = torBinary,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };
        _process.StartInfo.ArgumentList.Add("--SocksPort");
        _process.StartInfo.ArgumentList.Add(socksPort.ToString());
        _process.StartInfo.ArgumentList.Add("--ControlPort");
        _process.StartInfo.ArgumentList.Add(_controlPort.ToString());
        _process.StartInfo.ArgumentList.Add("--CookieAuthentication");
        _process.StartInfo.ArgumentList.Add("1");
        _process.StartInfo.ArgumentList.Add("--DataDirectory");
        _process.StartInfo.ArgumentList.Add(_dataDirectory);
        _process.StartInfo.ArgumentList.Add("--SafeSocks");
        _process.StartInfo.ArgumentList.Add("1");
        _process.StartInfo.ArgumentList.Add("--TestSocks");
        _process.StartInfo.ArgumentList.Add("1");
        _process.StartInfo.ArgumentList.Add("--Log");
        _process.StartInfo.ArgumentList.Add("notice stdout");

        _process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data?.Contains("Bootstrapped 100%", StringComparison.OrdinalIgnoreCase) == true)
            {
                bootstrapComplete.Set();
            }
        };
        _process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (!string.IsNullOrWhiteSpace(eventArgs.Data))
            {
                lock (errors)
                {
                    errors.Add(eventArgs.Data);
                }
            }
        };

        if (!_process.Start())
        {
            throw new InvalidOperationException("Could not start Tor.");
        }

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        if (!bootstrapComplete.Wait(TimeSpan.FromSeconds(90)))
        {
            var details = string.Join(Environment.NewLine, errors.TakeLast(10));
            Dispose();
            throw new TimeoutException($"Tor did not bootstrap within 90 seconds.{Environment.NewLine}{details}");
        }

        try
        {
            _exitIp = ReadExitIp();
        }
        catch
        {
            Dispose();
            throw;
        }
        Console.WriteLine($"Tor ready. SOCKS={ProxyServer}; exit={_exitIp}");
    }

    public string RotateIdentity()
    {
        var previous = _exitIp;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            SendControlCommand("SIGNAL NEWNYM");
            // Tor rate-limits NEWNYM; querying earlier usually returns the old exit.
            Thread.Sleep(TimeSpan.FromSeconds(10));
            var candidate = ReadExitIp();
            if (!string.Equals(candidate, previous, StringComparison.Ordinal))
            {
                _exitIp = candidate;
                Console.WriteLine($"Tor exit rotated: {previous} -> {candidate}");
                return candidate;
            }

            Console.WriteLine($"Tor returned the same exit on rotation attempt {attempt}: {candidate}");
        }

        throw new InvalidOperationException("Tor did not provide a different exit IP after three NEWNYM attempts.");
    }

    public TimeSpan Probe(string url)
    {
        var handler = new SocketsHttpHandler
        {
            Proxy = new WebProxy(ProxyServer),
            UseProxy = true,
            ConnectTimeout = TimeSpan.FromSeconds(8)
        };
        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 Kamui-Tor-Probe/1.0");

        var stopwatch = Stopwatch.StartNew();
        using var response = client.Send(
            new HttpRequestMessage(HttpMethod.Get, url),
            HttpCompletionOption.ResponseHeadersRead);
        stopwatch.Stop();
        response.EnsureSuccessStatusCode();
        return stopwatch.Elapsed;
    }

    string ReadExitIp()
    {
        var handler = new SocketsHttpHandler
        {
            Proxy = new WebProxy(ProxyServer),
            UseProxy = true,
            ConnectTimeout = TimeSpan.FromSeconds(20)
        };
        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Kamui-Tor-Check/1.0");
        var json = client.GetStringAsync("https://check.torproject.org/api/ip")
            .GetAwaiter()
            .GetResult();
        using var document = JsonDocument.Parse(json);
        var isTor = document.RootElement.GetProperty("IsTor").GetBoolean();
        var ip = document.RootElement.GetProperty("IP").GetString();
        if (!isTor || string.IsNullOrWhiteSpace(ip))
        {
            throw new InvalidOperationException("The local SOCKS endpoint is not exiting through Tor.");
        }

        return ip;
    }

    void SendControlCommand(string command)
    {
        var cookie = File.ReadAllBytes(_authenticationCookiePath);
        var authenticationToken = Convert.ToHexString(cookie);

        using var client = new TcpClient();
        client.ReceiveTimeout = 10_000;
        client.SendTimeout = 10_000;
        client.Connect(IPAddress.Loopback, _controlPort);
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true)
        {
            NewLine = "\r\n",
            AutoFlush = true
        };

        writer.WriteLine($"AUTHENTICATE {authenticationToken}");
        ExpectOk(reader, "AUTHENTICATE");
        writer.WriteLine(command);
        ExpectOk(reader, command);
        writer.WriteLine("QUIT");
    }

    static void ExpectOk(StreamReader reader, string command)
    {
        while (true)
        {
            var line = reader.ReadLine()
                ?? throw new IOException($"Tor closed the control connection during {command}.");
            if (line.StartsWith("250 ", StringComparison.Ordinal))
            {
                return;
            }

            if (line.Length >= 3 && char.IsDigit(line[0]) && line[0] != '2')
            {
                throw new InvalidOperationException($"Tor control command {command} failed: {line}");
            }
        }
    }

    static int FindFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    static string ResolveTorBinary()
    {
        var configured = Environment.GetEnvironmentVariable("TOR_BINARY");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        var candidates = new[]
        {
            "/opt/homebrew/opt/tor/bin/tor",
            "/opt/homebrew/bin/tor",
            "/usr/local/bin/tor",
            "/usr/bin/tor"
        };
        var binary = candidates.FirstOrDefault(File.Exists);
        return binary ?? throw new FileNotFoundException(
            "Tor is not installed. Install it with 'brew install tor' or set TOR_BINARY.");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            if (!_process.HasExited)
            {
                try { SendControlCommand("SIGNAL SHUTDOWN"); } catch { /* force-stop below */ }
                if (!_process.WaitForExit(5_000))
                {
                    _process.Kill(entireProcessTree: true);
                    _process.WaitForExit(5_000);
                }
            }
        }
        catch { /* process already exited */ }
        _process.Dispose();

        try { Directory.Delete(_dataDirectory, recursive: true); }
        catch { /* the OS can clean up a locked temporary directory */ }
    }
}

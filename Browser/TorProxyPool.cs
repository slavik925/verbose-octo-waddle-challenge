namespace Kamui.Browser;

/// <summary>
/// Starts independent Tor processes in parallel so every browser handoff has a
/// ready circuit. Exit /24s are kept distinct because multiple relays in one
/// network were observed to share the challenge's request quota.
/// </summary>
internal sealed class TorProxyPool : IDisposable
{
    private readonly List<Task<TorService>> _starters;
    private readonly List<TorService> _services = [];
    private readonly List<TorService> _deferred = [];
    private readonly List<TorService> _replacements = [];
    private readonly HashSet<string> _leasedExitNetworks = new(StringComparer.Ordinal);
    private bool _initialized;

    public TorProxyPool(int size)
    {
        if (size < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        Console.WriteLine($"Pre-warming {size} Tor proxies in parallel...");
        _starters = Enumerable.Range(0, size)
            .Select(_ => Task.Run(() => new TorService()))
            .ToList();
    }

    public TorService Acquire(string healthCheckUrl)
    {
        Initialize(healthCheckUrl);

        while (true)
        {
            while (_services.Count > 0)
            {
                var candidate = _services[0];
                _services.RemoveAt(0);

                for (var attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        var exitIp = candidate.ExitIp;
                        var exitNetwork = ExitNetwork(exitIp);
                        if (_leasedExitNetworks.Contains(exitNetwork))
                        {
                            Console.WriteLine(
                                $"Tor pool duplicate exit network {exitNetwork} ({exitIp}); " +
                                "requesting another circuit.");
                            candidate.RotateIdentity();
                            continue;
                        }

                        var latency = candidate.Probe(healthCheckUrl);
                        _leasedExitNetworks.Add(exitNetwork);
                        Console.WriteLine(
                            $"Tor proxy leased: exit={exitIp}; probe={latency.TotalMilliseconds:F0}ms");
                        return candidate;
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine($"Tor proxy health check {attempt} failed: {exception.Message}");
                        if (attempt < 3)
                        {
                            candidate.RotateIdentity();
                        }
                    }
                }

                // The process is still useful: a later NEWNYM can select a
                // different network without paying the Tor bootstrap cost.
                _deferred.Add(candidate);
            }

            if (_deferred.Count > 0)
            {
                Console.WriteLine($"Recycling {_deferred.Count} warm Tor proxies for fresh exit subnets.");
                _services.AddRange(_deferred);
                _deferred.Clear();
                continue;
            }

            Console.WriteLine("Tor pool exhausted; starting a replacement proxy.");
            Exception? lastError = null;
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    var replacement = new TorService();
                    _replacements.Add(replacement);
                    _services.Add(replacement);
                    break;
                }
                catch (Exception exception)
                {
                    lastError = exception;
                    Console.WriteLine($"Replacement Tor bootstrap {attempt}/3 failed: {exception.Message}");
                }
            }

            if (_services.Count == 0)
            {
                throw new InvalidOperationException("Could not start a replacement Tor proxy.", lastError);
            }
        }
    }

    void Initialize(string healthCheckUrl)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        var healthy = new List<(TorService Service, TimeSpan Latency)>();
        foreach (var starter in _starters)
        {
            try
            {
                var service = starter.GetAwaiter().GetResult();
                var latency = service.Probe(healthCheckUrl);
                healthy.Add((service, latency));
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Discarding Tor proxy: {exception.Message}");
            }
        }

        _services.AddRange(healthy
            .OrderBy(item => item.Latency)
            .Select(item => item.Service));
        Console.WriteLine($"Tor pool ready: {_services.Count}/{_starters.Count} healthy proxies.");
    }

    static string ExitNetwork(string address)
    {
        var ip = System.Net.IPAddress.Parse(address);
        var bytes = ip.GetAddressBytes();
        return bytes.Length == 4
            ? $"{bytes[0]}.{bytes[1]}.{bytes[2]}.0/24"
            : $"{Convert.ToHexString(bytes.AsSpan(0, 8))}/64";
    }

    public void Dispose()
    {
        foreach (var starter in _starters)
        {
            if (!starter.IsCompletedSuccessfully)
            {
                try { starter.Wait(TimeSpan.FromSeconds(5)); } catch { /* startup failed */ }
            }

            if (starter.IsCompletedSuccessfully)
            {
                try { starter.Result.Dispose(); } catch { /* already stopped */ }
            }
        }

        foreach (var replacement in _replacements)
        {
            try { replacement.Dispose(); } catch { /* already stopped */ }
        }
    }
}

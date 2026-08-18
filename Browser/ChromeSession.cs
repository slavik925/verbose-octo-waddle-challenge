using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using SeleniumUndetectedChromeDriver;

namespace Kamui.Browser;

internal sealed class ChromeSession : IDisposable
{
    public IWebDriver Driver { get; }
    public string UserAgent { get; private set; }

    public ChromeSession(string? userAgent = null, string? proxyServer = null)
    {
        UserAgent = userAgent ?? ChromeUserAgents.ForBatch(0);
        var driverPath = new ChromeDriverInstaller().Auto().GetAwaiter().GetResult();
        var options = new ChromeOptions
        {
            PageLoadStrategy = PageLoadStrategy.Eager
        };
        options.AddArgument("--password-store=basic");
        options.AddArgument("--disable-save-password-bubble");
        options.AddArgument("--disable-features=PasswordManagerOnboarding,PasswordCheck,PasswordLeakDetection");
        options.AddArgument("--blink-settings=imagesEnabled=false");
        options.AddArgument($"--user-agent={UserAgent}");
        if (!string.IsNullOrWhiteSpace(proxyServer))
        {
            options.AddArgument($"--proxy-server={proxyServer}");
            options.AddArgument("--disable-quic");
        }

        Driver = UndetectedChromeDriver.Create(
            options: options,
            driverExecutablePath: driverPath,
            prefs: new Dictionary<string, object>
            {
                ["credentials_enable_service"] = false,
                ["profile.password_manager_enabled"] = false,
                ["profile.password_manager_leak_detection"] = false,
                ["profile.managed_default_content_settings.images"] = 2
            });
        Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
        Driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(45);
        OverrideUserAgent();
        FitWindow();
    }

    void OverrideUserAgent(string acceptLanguage = "en-US,en;q=0.9")
    {
        if (Driver is not ChromeDriver chrome)
        {
            return;
        }

        var version = ChromeUserAgents.VersionFrom(UserAgent);
        var major = version.Split('.')[0];
        chrome.ExecuteCdpCommand("Emulation.setUserAgentOverride", new Dictionary<string, object?>
        {
            ["userAgent"] = UserAgent,
            ["acceptLanguage"] = acceptLanguage,
            ["platform"] = "MacIntel",
            ["userAgentMetadata"] = new Dictionary<string, object>
            {
                ["brands"] = new object[]
                {
                    new Dictionary<string, object> { ["brand"] = "Google Chrome", ["version"] = major },
                    new Dictionary<string, object> { ["brand"] = "Not:A-Brand", ["version"] = "8" },
                    new Dictionary<string, object> { ["brand"] = "Chromium", ["version"] = major }
                },
                ["fullVersion"] = version,
                ["platform"] = "macOS",
                ["platformVersion"] = "15.5.0",
                ["architecture"] = "arm",
                ["model"] = "",
                ["mobile"] = false
            }
        });
    }

    void SetUserAgent(string userAgent, string acceptLanguage)
    {
        UserAgent = userAgent;
        OverrideUserAgent(acceptLanguage);
    }

    public void RotateFingerprint(int batch)
    {
        if (Driver is not ChromeDriver chrome)
        {
            return;
        }

        var profile = FingerprintProfiles.ForBatch(batch);
        var acceptLanguage = FingerprintProfiles.AcceptLanguageForBatch(batch);
        var headers = FingerprintProfiles.HeadersForBatch(batch);
        SetUserAgent(profile.UserAgent, acceptLanguage);

        chrome.ExecuteCdpCommand("Emulation.setDeviceMetricsOverride", new Dictionary<string, object?>
        {
            ["width"] = profile.Width,
            ["height"] = profile.Height,
            ["deviceScaleFactor"] = profile.Scale,
            ["mobile"] = false,
            ["screenWidth"] = profile.Width,
            ["screenHeight"] = profile.Height
        });
        chrome.ExecuteCdpCommand("Emulation.setTimezoneOverride", new Dictionary<string, object?>
        {
            ["timezoneId"] = profile.Timezone
        });
        chrome.ExecuteCdpCommand("Emulation.setLocaleOverride", new Dictionary<string, object?>
        {
            ["locale"] = profile.Locale
        });
        chrome.ExecuteCdpCommand("Network.enable", new Dictionary<string, object?>());
        chrome.ExecuteCdpCommand("Network.setCacheDisabled", new Dictionary<string, object?>
        {
            ["cacheDisabled"] = true
        });
        chrome.ExecuteCdpCommand("Network.setBypassServiceWorker", new Dictionary<string, object?>
        {
            ["bypass"] = true
        });
        chrome.ExecuteCdpCommand("Network.setExtraHTTPHeaders", new Dictionary<string, object?>
        {
            ["headers"] = headers
        });
        chrome.ExecuteCdpCommand("Page.addScriptToEvaluateOnNewDocument", new Dictionary<string, object?>
        {
            ["source"] = FingerprintProfiles.CanvasNoiseScript(batch)
        });

        Driver.Manage().Window.Size = new System.Drawing.Size(profile.Width, profile.Height);
        var headerSummary = string.Join(", ", headers.Select(pair => $"{pair.Key}={pair.Value}"));
        Console.WriteLine($"Fingerprint: {profile.Width}x{profile.Height}@{profile.Scale} {profile.Timezone} {profile.Locale}");
        Console.WriteLine($"Headers: {headerSummary}");
    }

    public IReadOnlyList<Cookie> SnapshotCookies() =>
        Driver.Manage().Cookies.AllCookies
            .Select(c => new Cookie(
                c.Name,
                c.Value,
                c.Domain,
                c.Path,
                c.Expiry,
                c.Secure,
                c.IsHttpOnly,
                c.SameSite))
            .ToList();

    public int RestoreCookies(IEnumerable<Cookie> cookies, string originUrl)
    {
        Driver.Navigate().GoToUrl(originUrl);
        Pause.Between(400, 800);

        // Visiting the origin creates a temporary session. Delete it before
        // restoring the challenge cookie to avoid duplicate host/domain scopes.
        Driver.Manage().Cookies.DeleteAllCookies();

        if (Driver is not ChromeDriver chrome)
        {
            return 0;
        }

        var restored = 0;
        foreach (var cookie in cookies)
        {
            if (cookie.Name.Contains("Antiforgery", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                var parameters = new Dictionary<string, object?>
                {
                    ["name"] = cookie.Name,
                    ["value"] = cookie.Value,
                    ["path"] = cookie.Path ?? "/",
                    ["secure"] = cookie.Secure,
                    ["httpOnly"] = cookie.IsHttpOnly,
                    ["sameSite"] = cookie.SameSite ?? "Lax"
                };

                var domain = cookie.Domain ?? string.Empty;
                if (domain.StartsWith(".", StringComparison.Ordinal))
                {
                    parameters["domain"] = domain;
                }
                else
                {
                    // CDP's url field preserves the host-only attribute. Selenium's
                    // AddCookie would turn this into a domain cookie and break the session.
                    parameters["url"] = originUrl;
                }

                if (cookie.Expiry is not null)
                {
                    parameters["expires"] = new DateTimeOffset(cookie.Expiry.Value).ToUnixTimeSeconds();
                }

                var result = chrome.ExecuteCdpCommand("Network.setCookie", parameters);
                if (result is Dictionary<string, object> response
                    && response.TryGetValue("success", out var success)
                    && Convert.ToBoolean(success))
                {
                    restored++;
                }
            }
            catch
            {
                // expired or domain mismatch
            }
        }

        return restored;
    }

    void FitWindow()
    {
        var sizes = new[]
        {
            (1280, 800), (1366, 768), (1440, 900), (1512, 982), (1680, 1050)
        };
        var (width, height) = sizes[Random.Shared.Next(sizes.Length)];
        Driver.Manage().Window.Size = new System.Drawing.Size(width, height);
        Pause.Between(200, 500);
        Driver.Manage().Window.Size = new System.Drawing.Size(
            width + Random.Shared.Next(-24, 25),
            height + Random.Shared.Next(-16, 17));
    }

    public void Dispose()
    {
        try { Driver.Quit(); } catch { /* chrome already closed */ }
        try { Driver.Dispose(); } catch { /* already disposed */ }
    }
}

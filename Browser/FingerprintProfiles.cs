namespace Kamui.Browser;

internal static class FingerprintProfiles
{
    private static readonly (int Width, int Height, int Scale)[] Screens =
    [
        (1280, 800, 2),
        (1440, 900, 2),
        (1920, 1080, 1),
        (1512, 982, 2),
        (1366, 768, 1)
    ];

    private static readonly string[] Timezones =
    [
        "America/Toronto",
        "America/New_York",
        "America/Chicago",
        "America/Vancouver",
        "America/Denver"
    ];

    private static readonly string[] Locales =
    [
        "en-CA",
        "en-US",
        "fr-CA",
        "en-GB",
        "en-AU"
    ];

    private static readonly string[] AcceptLanguages =
    [
        "en-CA,en;q=0.9,fr-CA;q=0.7",
        "en-US,en;q=0.9",
        "fr-CA,fr;q=0.9,en-CA;q=0.7,en;q=0.6",
        "en-GB,en;q=0.9",
        "en-AU,en;q=0.9"
    ];

    private static readonly string[] Accepts =
    [
        "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7",
        "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/avif,*/*;q=0.7",
        "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8",
        "text/html,application/xhtml+xml,application/xml;q=0.8,image/webp,*/*;q=0.7",
        "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8"
    ];

    private static readonly string[] AcceptEncodings =
    [
        "gzip, deflate, br, zstd",
        "gzip, deflate, br",
        "gzip, deflate, br, zstd",
        "gzip, deflate, br",
        "gzip, deflate, br, zstd"
    ];

    public static (int Width, int Height, int Scale, string Timezone, string Locale, string UserAgent) ForBatch(int batch)
    {
        var i = batch % Screens.Length;
        var screen = Screens[i];
        return (
            screen.Width,
            screen.Height,
            screen.Scale,
            Timezones[i],
            Locales[i],
            ChromeUserAgents.ForBatch(batch));
    }

    public static string AcceptLanguageForBatch(int batch) =>
        AcceptLanguages[batch % AcceptLanguages.Length];

    public static IReadOnlyDictionary<string, object> HeadersForBatch(int batch)
    {
        var i = batch % AcceptLanguages.Length;
        var headers = new Dictionary<string, object>
        {
            ["Accept"] = Accepts[i],
            ["Accept-Encoding"] = AcceptEncodings[i],
            ["Accept-Language"] = AcceptLanguages[i],
            ["Upgrade-Insecure-Requests"] = "1",
            ["Priority"] = i % 2 == 0 ? "u=0, i" : "u=1, i"
        };

        if (batch is 0 or 1 or 2 or 4)
        {
            headers["DNT"] = "1";
            headers["Sec-GPC"] = "1";
        }

        if (batch is 1 or 3)
        {
            headers["Cache-Control"] = "no-cache";
            headers["Pragma"] = "no-cache";
        }

        if (batch == 1)
        {
            headers["Save-Data"] = "on";
        }

        return headers;
    }

    public static string CanvasNoiseScript(int batch) =>
        $$"""
        (() => {
          const seed = {{batch + 1}};
          const toDataURL = HTMLCanvasElement.prototype.toDataURL;
          HTMLCanvasElement.prototype.toDataURL = function() {
            const ctx = this.getContext('2d');
            if (ctx) {
              ctx.fillStyle = 'rgba(' + seed + ',3,7,0.01)';
              ctx.fillRect(seed, seed, 2, 2);
            }
            return toDataURL.apply(this, arguments);
          };
          const proto = WebGLRenderingContext.prototype;
          const getParameter = proto.getParameter;
          proto.getParameter = function(p) {
            const value = getParameter.apply(this, arguments);
            if (p === 37445) return 'Google Inc. (Apple)';
            if (p === 37446) return 'ANGLE (Apple, ANGLE Metal Renderer: Apple M' + seed + ', Unspecified Version)';
            return value;
          };
        })();
        """;
}

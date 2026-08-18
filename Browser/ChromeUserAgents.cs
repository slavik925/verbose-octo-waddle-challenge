namespace Kamui.Browser;

internal static class ChromeUserAgents
{
    // Keep the browser family stable while varying the patch per generation.
    private static readonly string[] Builds =
    [
        "151.0.7922.138",
        "151.0.7922.141",
        "151.0.7922.144",
        "151.0.7922.149",
        "151.0.7922.153"
    ];

    public static string ForBatch(int batch) =>
        $"Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{Builds[batch % Builds.Length]} Safari/537.36";

    public static string VersionFrom(string userAgent)
    {
        const string marker = "Chrome/";
        var start = userAgent.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return "151.0.7922.138";
        }

        start += marker.Length;
        var end = userAgent.IndexOf(' ', start);
        return end < 0 ? userAgent[start..] : userAgent[start..end];
    }
}

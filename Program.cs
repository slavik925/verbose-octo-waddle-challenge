using Kamui.Collection;
using Kamui.Configuration;
using Kamui.Discovery;
using Kamui.Models;

var settings = ChallengeSettings.Default;
var store = new TokenStore(settings.OutputPath);

Credentials credentials;

if (args.Contains("--known", StringComparer.OrdinalIgnoreCase))
{
    credentials = new Credentials(settings.Login, settings.Password);
    Console.WriteLine($"Using known credentials: {credentials}");
}
else
{
    // Both values match [1-3]{1,4}; equal pairs are tried before the full grid.
    Console.WriteLine("Discovering credentials via brute force...");
    var discovered = new CredentialFinder().Find();
    if (discovered is null)
    {
        Console.WriteLine("No credentials found.");
        return;
    }

    credentials = discovered.Value;
    Console.WriteLine($"Found {credentials}");
}

Console.WriteLine($"Collecting {settings.TokenTarget} tokens → {settings.OutputPath}");
var tokens = new TokenCollector(settings, store).Collect(credentials);

Console.WriteLine($"Done. tokens={tokens.Count}/{settings.TokenTarget}");
Console.WriteLine(settings.OutputPath);
Environment.ExitCode = tokens.Count == settings.TokenTarget ? 0 : 1;

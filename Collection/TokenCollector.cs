using Kamui.Browser;
using Kamui.Configuration;
using Kamui.Models;
using Kamui.Pages;

namespace Kamui.Collection;

internal sealed class TokenCollector
{
    private readonly ChallengeSettings _settings;
    private readonly TokenStore _store;

    // Successful logins per Chrome/Tor exit before the server flags the session.
    private const int BrowserTokenLimit = 10;
    // Retries (with a new browser/exit) when a submit does not yield a token.
    private const int MaxSubmissionAttempts = 10;
    // Occurrence where the page requires 10 mousemoves + GET /mm before sign-in.
    private const int MouseSignalStart = 41;

    public TokenCollector(ChallengeSettings settings, TokenStore store)
    {
        _settings = settings;
        _store = store;
    }

    public IReadOnlyList<string> Collect(Credentials credentials)
    {
        // Tor processes warm up while the direct browser collects its first batch.
        using var torPool = new TorProxyPool(8);
        var session = new ChromeSession(ChromeUserAgents.ForBatch(0));

        try
        {
            var landing = new LandingPage(session.Driver);
            landing.Open(LandingPage.HomeUrl);
            landing.Start();

            var page = new AuthorizePage(session.Driver);
            var challengeId = page.ChallengeId;
            if (challengeId is null)
            {
                Fail(1, "START did not create a ChallengeId", page);
                return [];
            }

            if (!Pause.Until(() => page.HasLoginForm || page.IsBlocked, TimeSpan.FromSeconds(30)))
            {
                Fail(1, "login form missing", page);
                return [];
            }

            Console.WriteLine($"Session ChallengeId: {challengeId}");
            Console.WriteLine($"UA: {session.UserAgent}");

            var tokens = new List<string>(_settings.TokenTarget);
            var browserGeneration = 0;
            var tokensOnCurrentBrowser = 0;

            bool SwitchBrowser(int nextOccurrence)
            {
                for (var attempt = 1; attempt <= 3; attempt++)
                {
                    // The challenge ID lives in the encrypted ASP.NET session.
                    // Copying that host-only cookie keeps every token under one ID.
                    var cookies = session.SnapshotCookies();
                    session.Dispose();

                    browserGeneration++;
                    var tor = torPool.Acquire(LandingPage.HomeUrl);
                    session = new ChromeSession(
                        ChromeUserAgents.ForBatch(browserGeneration),
                        tor.ProxyServer);
                    session.RotateFingerprint(browserGeneration);

                    var restoredCookies = session.RestoreCookies(cookies, LandingPage.HomeUrl);
                    page = new AuthorizePage(session.Driver);
                    page.Open($"https://challenge.flinks.com/Authorize/{challengeId}");

                    if (!Pause.Until(() => page.HasLoginForm || page.IsBlocked, TimeSpan.FromSeconds(45)))
                    {
                        Fail(nextOccurrence, "new browser could not load login form", page);
                        return false;
                    }

                    Console.WriteLine(
                        $"Browser {browserGeneration}: Tor exit={tor.ExitIp}; " +
                        $"cookies={restoredCookies}; UA={session.UserAgent}");

                    if (!page.IsBlocked)
                    {
                        tokensOnCurrentBrowser = 0;
                        return true;
                    }

                    Console.WriteLine(
                        $"Exit {tor.ExitIp} is already blocked; trying another proxy ({attempt}/3).");
                }

                return false;
            }

            for (var occurrence = 1; occurrence <= _settings.TokenTarget; occurrence++)
            {
                Pause.Between(50, 125);

                if (!page.HasLoginForm)
                {
                    Fail(occurrence, "login form missing", page);
                    break;
                }

                var collected = false;
                for (var attempt = 1; attempt <= MaxSubmissionAttempts; attempt++)
                {
                    var formNonce = page.FormNonce;
                    var submitted = page.SignIn(
                        credentials.Login,
                        credentials.Password,
                        requireMouseMovement: occurrence >= MouseSignalStart);

                    if (submitted && TryRead(page, occurrence, challengeId, tokens, formNonce))
                    {
                        collected = true;
                        break;
                    }

                    if (page.IsWrongCredential)
                    {
                        break;
                    }

                    var reason = submitted
                        ? page.IsBlocked ? "exit blocked" : "response did not contain a token"
                        : "interaction signal failed";
                    Console.WriteLine(
                        $"[{occurrence}/{_settings.TokenTarget}] {reason}; " +
                        $"switching browser before retry {attempt + 1}/{MaxSubmissionAttempts}.");

                    if (attempt == MaxSubmissionAttempts || !SwitchBrowser(occurrence))
                    {
                        break;
                    }
                }

                if (!collected)
                {
                    Fail(occurrence, "login could not be completed after retries", page);
                    break;
                }

                tokensOnCurrentBrowser++;
                if (tokensOnCurrentBrowser >= BrowserTokenLimit && occurrence < _settings.TokenTarget)
                {
                    if (!SwitchBrowser(occurrence + 1))
                    {
                        break;
                    }

                    continue;
                }

                if (occurrence < _settings.TokenTarget && !page.BackToLogin())
                {
                    Fail(occurrence + 1, "could not return to login form", page);
                    break;
                }
            }

            return tokens;
        }
        finally
        {
            session.Dispose();
        }
    }

    bool TryRead(
        AuthorizePage page,
        int attempt,
        string challengeId,
        List<string> tokens,
        string? submittedFormNonce)
    {
        var responseReady = Pause.Until(
            () => page.HasToken
                || page.IsBlocked
                || page.IsWrongCredential
                || (page.HasLoginForm
                    && !string.Equals(page.FormNonce, submittedFormNonce, StringComparison.Ordinal)),
            TimeSpan.FromSeconds(30));

        if (!responseReady || page.IsBlocked || page.IsWrongCredential || page.HasLoginForm)
        {
            return false;
        }

        var token = page.ReadToken();
        if (token is null)
        {
            return false;
        }

        tokens.Add(token.Value.Token);
        _store.Save(challengeId, tokens);
        Console.WriteLine(
            $"[{attempt}/{_settings.TokenTarget}] occurrence={token.Value.Occurrence} " +
            $"token={token.Value.Token}");
        return true;
    }

    void Fail(int occurrence, string reason, AuthorizePage page)
    {
        Console.WriteLine($"[{occurrence}/{_settings.TokenTarget}] {reason}. url={page.Url}");
        Console.WriteLine(page.Body);
    }
}

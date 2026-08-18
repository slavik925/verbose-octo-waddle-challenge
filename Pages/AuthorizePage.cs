using System.Text.RegularExpressions;
using OpenQA.Selenium;
using Kamui.Browser;
using Kamui.Models;

namespace Kamui.Pages;

internal sealed class AuthorizePage(IWebDriver driver) : PageBase(driver)
{
    private static readonly Regex TokenRegex = new(@"Authentification token:\s*(\S+)", RegexOptions.Compiled);
    private static readonly Regex OccurrenceRegex = new(@"Occurence:\s*(\d+)", RegexOptions.Compiled);
    private static readonly Regex ChallengeIdRegex = new(@"/Authorize/(\d+)", RegexOptions.Compiled);

    public bool HasLoginForm
    {
        get
        {
            try
            {
                var script = (IJavaScriptExecutor)Driver;
                return Convert.ToBoolean(script.ExecuteScript(
                    "return Boolean(document.querySelector('form input[type=\"text\"]') && " +
                    "document.querySelector('form input[type=\"password\"]') && " +
                    "document.querySelector('form button[type=\"submit\"]'));"));
            }
            catch (WebDriverException)
            {
                return false;
            }
        }
    }
    public bool HasToken => TokenRegex.IsMatch(Body);
    public string? FormNonce
    {
        get
        {
            try
            {
                return Driver.FindElement(By.Name("__RequestVerificationToken"))
                    .GetAttribute("value");
            }
            catch (WebDriverException)
            {
                return null;
            }
        }
    }
    public bool IsWrongCredential =>
        Body.Contains("Wrong credential", StringComparison.OrdinalIgnoreCase);

    public string? ChallengeId
    {
        get
        {
            var match = ChallengeIdRegex.Match(Url);
            return match.Success ? match.Groups[1].Value : null;
        }
    }

    public bool SignIn(string login, string password, bool requireMouseMovement = false)
    {
        // The challenge deliberately renames these fields after occurrence 31
        // (username2/password2), so locate them by semantic input type.
        var username = By.CssSelector("form input[type='text']");
        var passwordField = By.CssSelector("form input[type='password']");
        var submit = By.CssSelector("form button[type='submit']");

        if (!Pause.Until(() => HasLoginForm, TimeSpan.FromSeconds(15)))
        {
            return false;
        }

        if (requireMouseMovement && !SatisfyMouseMovementGate())
        {
            return false;
        }

        Type(username, login);
        Type(passwordField, password);
        try
        {
            Click(submit);
        }
        catch (WebDriverTimeoutException)
        {
            // Chrome may time out waiting for a slow Tor response after the POST
            // was already sent. Let the response reader inspect/recover it.
        }
        return true;
    }

    public bool BackToLogin()
    {
        // Reloading the challenge route is equivalent to BACK, but avoids an
        // unreliable delayed link click over high-latency Tor circuits.
        Driver.Navigate().GoToUrl(Url);
        return Pause.Until(() => HasLoginForm, TimeSpan.FromSeconds(60));
    }

    public AuthToken? ReadToken()
    {
        var text = Body;
        var token = TokenRegex.Match(text);
        if (!token.Success)
        {
            return null;
        }

        var occurrence = OccurrenceRegex.Match(text);
        return new AuthToken(
            occurrence.Success ? int.Parse(occurrence.Groups[1].Value) : 0,
            token.Groups[1].Value);
    }
}

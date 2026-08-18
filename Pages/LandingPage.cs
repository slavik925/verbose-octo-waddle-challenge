using OpenQA.Selenium;
using Kamui.Browser;

namespace Kamui.Pages;

internal sealed class LandingPage(IWebDriver driver) : PageBase(driver)
{
    public const string HomeUrl = "https://challenge.flinks.com/";

    public void Start()
    {
        Click(By.LinkText("START"));
        Pause.Until(() => Driver.Url.Contains("/Authorize/", StringComparison.Ordinal), TimeSpan.FromSeconds(30));
    }
}

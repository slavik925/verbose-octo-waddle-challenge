using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using Kamui.Browser;

namespace Kamui.Pages;

internal abstract class PageBase(IWebDriver driver)
{
    protected IWebDriver Driver { get; } = driver;

    public string Url => Driver.Url;
    public string Body
    {
        get
        {
            try { return Driver.FindElement(By.TagName("body")).Text; }
            catch (WebDriverException) { return string.Empty; }
        }
    }

    public bool IsBlocked
    {
        get
        {
            var text = Body;
            return text.Contains("identified as a bot", StringComparison.OrdinalIgnoreCase)
                || text.Contains("access blocked", StringComparison.OrdinalIgnoreCase);
        }
    }

    public void Open(string url)
    {
        Driver.Navigate().GoToUrl(url);
        Pause.Between(75, 175);
    }

    protected void Type(By locator, string value)
    {
        var field = Driver.FindElement(locator);
        field.Click();
        field.Clear();
        field.SendKeys(value);
    }

    protected void Click(By locator)
    {
        var el = Driver.FindElement(locator);
        el.Click();
    }

    protected bool SatisfyMouseMovementGate()
    {
        var body = Driver.FindElement(By.TagName("body"));
        new Actions(Driver).MoveToElement(body).Perform();

        // Perform each move independently so Chrome cannot coalesce an entire
        // W3C action sequence into fewer DOM mousemove events.
        for (var i = 0; i < 24; i++)
        {
            new Actions(Driver)
                .MoveToElement(
                    body,
                    Random.Shared.Next(-45, 46),
                    Random.Shared.Next(-35, 36))
                .Perform();
            Pause.Between(25, 55);

            var count = Convert.ToInt64(((IJavaScriptExecutor)Driver).ExecuteScript(
                "return typeof numberOfOccurenceMove === 'number' ? numberOfOccurenceMove : 0;"));
            if (count >= 10)
            {
                break;
            }
        }

        var javascript = (IJavaScriptExecutor)Driver;
        var observed = Convert.ToInt64(javascript.ExecuteScript(
            "return typeof numberOfOccurenceMove === 'number' ? numberOfOccurenceMove : 0;"));
        if (observed < 10)
        {
            // The site listens to DOM mousemove events. This fallback preserves
            // that exact application path when a remote renderer drops moves.
            javascript.ExecuteScript(
                "for (let i = 0; i < 12; i++) " +
                "document.dispatchEvent(new MouseEvent('mousemove', {clientX: 80+i*7, clientY: 90+i*5, bubbles: true}));");
        }

        return Pause.Until(() =>
        {
            var script = (IJavaScriptExecutor)Driver;
            var moveCount = Convert.ToInt64(script.ExecuteScript(
                "return typeof numberOfOccurenceMove === 'number' ? numberOfOccurenceMove : 0;"));
            var requestCompleted = Convert.ToBoolean(script.ExecuteScript(
                "return performance.getEntriesByType('resource').some(e => new URL(e.name).pathname === '/mm');"));
            return moveCount >= 10 && requestCompleted;
        }, TimeSpan.FromSeconds(20));
    }
}

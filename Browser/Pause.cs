namespace Kamui.Browser;

internal static class Pause
{
    public static void Between(int minMs, int maxMs) =>
        Thread.Sleep(Random.Shared.Next(minMs, maxMs));

    public static bool Until(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (condition())
                {
                    return true;
                }
            }
            catch (Exception)
            {
                // page may be mid-navigation
            }

            Thread.Sleep(200);
        }

        return false;
    }
}

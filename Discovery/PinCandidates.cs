using Kamui.Models;

namespace Kamui.Discovery;

// Username and password both match [1-3]{1,4}. Popular PINs first, then the rest.
internal static class PinCandidates
{
    private static readonly string[] Popular =
    [
        "123", "1111", "1212", "1", "11", "111", "12",
        "2222", "3333", "1122", "1221", "321", "1313",
        "1123", "1233", "1231", "1232", "22", "222", "2",
        "33", "333", "3", "21", "13", "121", "2121"
    ];

    private static readonly HashSet<string> PopularSet = [.. Popular];

    public static IEnumerable<string> MatchingRegex()
    {
        foreach (var pin in Popular)
        {
            yield return pin;
        }

        foreach (var pin in Generate())
        {
            if (!PopularSet.Contains(pin))
            {
                yield return pin;
            }
        }
    }

    public static IEnumerable<Credentials> EqualPairs() =>
        MatchingRegex().Select(pin => new Credentials(pin, pin));

    public static IEnumerable<Credentials> UnequalPairs()
    {
        var pins = MatchingRegex().ToArray();
        foreach (var login in pins)
        {
            foreach (var password in pins)
            {
                if (login != password)
                {
                    yield return new Credentials(login, password);
                }
            }
        }
    }

    static IEnumerable<string> Generate()
    {
        const string digits = "123";
        for (var length = 1; length <= 4; length++)
        {
            foreach (var pin in Combinations(digits, length))
            {
                yield return pin;
            }
        }
    }

    static IEnumerable<string> Combinations(string digits, int length)
    {
        if (length == 1)
        {
            foreach (var digit in digits)
            {
                yield return digit.ToString();
            }

            yield break;
        }

        foreach (var head in digits)
        {
            foreach (var tail in Combinations(digits, length - 1))
            {
                yield return head + tail;
            }
        }
    }
}

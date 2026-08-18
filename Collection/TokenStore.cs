namespace Kamui.Collection;

internal sealed class TokenStore
{
    private readonly string _path;

    public TokenStore(string path)
    {
        _path = path;
    }

    public void Save(string challengeId, IReadOnlyList<string> tokens)
    {
        var lines = new List<string>
        {
            $"ChallengeId: {challengeId}",
            $"Count: {tokens.Count}",
            ""
        };
        lines.AddRange(tokens.Select((token, i) => $"{i + 1}. {token}"));
        File.WriteAllLines(_path, lines);
    }
}

namespace Kamui.Configuration;

internal sealed class ChallengeSettings
{
    public required string Login { get; init; }
    public required string Password { get; init; }
    public required int TokenTarget { get; init; }
    public required string OutputPath { get; init; }

    public static ChallengeSettings Default { get; } = new()
    {
        Login = "2222",
        Password = "2222",
        TokenTarget = 50,
        OutputPath = Path.GetFullPath("tokens.txt")
    };
}

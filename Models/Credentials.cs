namespace Kamui.Models;

internal readonly record struct Credentials(string Login, string Password)
{
    public override string ToString() => $"{Login} / {Password}";
}

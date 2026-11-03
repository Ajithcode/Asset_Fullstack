namespace A360.UserAccount.Api.Contracts;

internal static class GeneratedIdentifier
{
    public static string Create(string prefix)
    {
        return $"{prefix}{DateTime.UtcNow.Ticks}";
    }
}

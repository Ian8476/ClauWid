namespace ClaudeUsageWidget.Infrastructure.ClaudeCode;

/// <summary>Lo que habia en .credentials.json al mirarlo. Sin token util, AccessToken es null.</summary>
public sealed record AccessTokenReading(AccessTokenState State, string? AccessToken = null)
{
    public static readonly AccessTokenReading Missing = new(AccessTokenState.Missing);

    public static readonly AccessTokenReading Expired = new(AccessTokenState.Expired);

    public static AccessTokenReading Usable(string accessToken) => new(AccessTokenState.Usable, accessToken);
}

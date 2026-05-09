namespace Bcs.InvestApi.Tokens;

internal sealed record BcsAccessTokenInfo
{
    public required string AccessToken { get; init; }

    public required DateTimeOffset AccessTokenExpiresAtUtc { get; init; }

    public string TokenType { get; init; } = "Bearer";
}

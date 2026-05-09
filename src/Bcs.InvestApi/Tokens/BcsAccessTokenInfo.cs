namespace Bcs.InvestApi.Tokens;

public sealed record BcsAccessTokenInfo
{
    public required string AccessToken { get; init; }

    public required DateTimeOffset AccessTokenExpiresAtUtc { get; init; }

    public string TokenType { get; init; } = "Bearer";
}

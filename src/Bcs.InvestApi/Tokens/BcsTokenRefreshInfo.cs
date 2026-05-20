namespace Bcs.InvestApi.Tokens;

/// <summary>
/// Payload passed to <see cref="BcsInvestApiSettings.OnTokenRefreshed"/> after a successful
/// refresh-token exchange. Does not carry any token values, only timing and provenance metadata.
/// </summary>
public sealed record BcsTokenRefreshInfo
{
    /// <summary>Which refresh token produced the new token pair.</summary>
    public required BcsRefreshTokenSource Source { get; init; }

    /// <summary>When the new token pair was received.</summary>
    public required DateTimeOffset ReceivedAtUtc { get; init; }

    /// <summary>When the new access token expires.</summary>
    public required DateTimeOffset AccessTokenExpiresAtUtc { get; init; }

    /// <summary>When the new refresh token expires.</summary>
    public required DateTimeOffset RefreshTokenExpiresAtUtc { get; init; }

    /// <summary>OAuth scope returned with the token pair.</summary>
    public required string Scope { get; init; }
}

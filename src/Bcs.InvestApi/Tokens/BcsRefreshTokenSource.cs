namespace Bcs.InvestApi.Tokens;

/// <summary>
/// Indicates which refresh token the SDK used for a refresh-token exchange.
/// </summary>
public enum BcsRefreshTokenSource
{
    /// <summary>
    /// The stable bootstrap refresh token supplied via <see cref="BcsInvestApiSettings.RefreshToken"/>.
    /// Used on the first refresh or after the in-memory rotated token has expired or was rejected.
    /// </summary>
    Bootstrap,

    /// <summary>
    /// The rotated refresh token previously returned by the auth server and held in memory.
    /// </summary>
    Rotated,
}

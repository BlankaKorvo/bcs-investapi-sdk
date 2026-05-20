namespace Bcs.InvestApi.Tokens;

/// <summary>
/// Payload passed to <see cref="BcsInvestApiSettings.OnTokenRefreshFailed"/> when a refresh-token
/// exchange does not produce a usable token pair (auth error, timeout, transport failure).
/// </summary>
public sealed record BcsTokenRefreshFailure
{
    /// <summary>Which refresh token the failed exchange attempted to use.</summary>
    public required BcsRefreshTokenSource Source { get; init; }

    /// <summary>The exception that aborted the exchange.</summary>
    public required Exception Exception { get; init; }
}

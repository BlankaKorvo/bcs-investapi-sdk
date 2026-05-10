namespace Bcs.InvestApi.Tokens;

using Bcs.InvestApi.Contracts.Auth;

internal sealed record BcsTokenSet
{
    public required string AccessToken { get; init; }

    public required string RefreshToken { get; init; }

    public string TokenType { get; init; } = "bearer";

    public long ExpiresIn { get; init; }

    public long RefreshExpiresIn { get; init; }

    public DateTimeOffset AccessTokenExpiresAtUtc { get; init; }

    public DateTimeOffset RefreshTokenExpiresAtUtc { get; init; }

    public DateTimeOffset ReceivedAtUtc { get; init; }

    public long NotBeforePolicy { get; init; }

    public string SessionState { get; init; } = string.Empty;

    public string Scope { get; init; } = string.Empty;

    public bool ShouldRefreshAccessToken(DateTimeOffset nowUtc, TimeSpan refreshSkew)
    {
        if (refreshSkew < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(refreshSkew), "Refresh skew must be greater than or equal to zero.");
        }

        return nowUtc.Add(refreshSkew) >= AccessTokenExpiresAtUtc;
    }

    internal bool HasUsableAccessToken(DateTimeOffset nowUtc, TimeSpan refreshSkew)
    {
        if (string.IsNullOrWhiteSpace(AccessToken))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(TokenType))
        {
            return false;
        }

        return !ShouldRefreshAccessToken(nowUtc, refreshSkew);
    }

    public bool IsRefreshTokenExpired(DateTimeOffset nowUtc, TimeSpan refreshSkew)
    {
        if (refreshSkew < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(refreshSkew), "Refresh skew must be greater than or equal to zero.");
        }

        return nowUtc.Add(refreshSkew) >= RefreshTokenExpiresAtUtc;
    }

    internal bool HasUsableRefreshToken(DateTimeOffset nowUtc, TimeSpan refreshSkew) =>
        !string.IsNullOrWhiteSpace(RefreshToken) &&
        !IsRefreshTokenExpired(nowUtc, refreshSkew);

    internal static BcsTokenSet FromAuthResponse(BcsAuthResponse response, DateTimeOffset receivedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Validate();

        return new BcsTokenSet
        {
            AccessToken = response.AccessToken,
            RefreshToken = response.RefreshToken,
            TokenType = response.TokenType,
            ExpiresIn = response.ExpiresIn,
            RefreshExpiresIn = response.RefreshExpiresIn,
            AccessTokenExpiresAtUtc = receivedAtUtc.AddSeconds(response.ExpiresIn),
            RefreshTokenExpiresAtUtc = receivedAtUtc.AddSeconds(response.RefreshExpiresIn),
            ReceivedAtUtc = receivedAtUtc,
            NotBeforePolicy = response.NotBeforePolicy,
            SessionState = response.SessionState,
            Scope = response.Scope,
        };
    }
}

namespace Bcs.InvestApi.Tokens;

using System.Text.Json.Serialization;
using Bcs.InvestApi.Auth;

public sealed record BcsTokenSet
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("refresh_token")]
    public required string RefreshToken { get; init; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; init; } = "bearer";

    [JsonPropertyName("expires_in")]
    public long ExpiresIn { get; init; }

    [JsonPropertyName("refresh_expires_in")]
    public long RefreshExpiresIn { get; init; }

    [JsonPropertyName("access_token_expires_at_utc")]
    public DateTimeOffset AccessTokenExpiresAtUtc { get; init; }

    [JsonPropertyName("refresh_token_expires_at_utc")]
    public DateTimeOffset RefreshTokenExpiresAtUtc { get; init; }

    [JsonPropertyName("received_at_utc")]
    public DateTimeOffset ReceivedAtUtc { get; init; }

    [JsonPropertyName("not-before-policy")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long NotBeforePolicy { get; init; }

    [JsonPropertyName("session_state")]
    public string SessionState { get; init; } = string.Empty;

    [JsonPropertyName("scope")]
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

    internal bool HasUsableRefreshToken(DateTimeOffset nowUtc) =>
        !string.IsNullOrWhiteSpace(RefreshToken) &&
        !IsRefreshTokenExpired(nowUtc, TimeSpan.Zero);

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

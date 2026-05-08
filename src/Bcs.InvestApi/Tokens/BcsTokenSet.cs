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

    public bool IsRefreshTokenExpired(DateTimeOffset nowUtc, TimeSpan refreshSkew)
    {
        if (refreshSkew < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(refreshSkew), "Refresh skew must be greater than or equal to zero.");
        }

        return nowUtc.Add(refreshSkew) >= RefreshTokenExpiresAtUtc;
    }

    internal static BcsTokenSet FromAuthResponse(BcsAuthResponse response, DateTimeOffset receivedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (string.IsNullOrWhiteSpace(response.AccessToken))
        {
            throw new InvalidOperationException("BCS auth response does not contain access_token.");
        }

        if (string.IsNullOrWhiteSpace(response.RefreshToken))
        {
            throw new InvalidOperationException("BCS auth response does not contain refresh_token.");
        }

        if (response.ExpiresIn <= 0)
        {
            throw new InvalidOperationException($"BCS auth response expires_in must be greater than zero. Actual value: {response.ExpiresIn}.");
        }

        if (response.RefreshExpiresIn <= 0)
        {
            throw new InvalidOperationException($"BCS auth response refresh_expires_in must be greater than zero. Actual value: {response.RefreshExpiresIn}.");
        }

        return new BcsTokenSet
        {
            AccessToken = response.AccessToken,
            RefreshToken = response.RefreshToken,
            TokenType = string.IsNullOrWhiteSpace(response.TokenType) ? "bearer" : response.TokenType,
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

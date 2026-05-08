namespace Bcs.InvestApi.Auth;

using System.Text.Json.Serialization;

public sealed record BcsAuthResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public long ExpiresIn { get; init; }

    [JsonPropertyName("refresh_expires_in")]
    public long RefreshExpiresIn { get; init; }

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; init; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; init; } = string.Empty;

    [JsonPropertyName("not-before-policy")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long NotBeforePolicy { get; init; }

    [JsonPropertyName("session_state")]
    public string SessionState { get; init; } = string.Empty;

    [JsonPropertyName("scope")]
    public string Scope { get; init; } = string.Empty;

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(AccessToken))
        {
            throw new InvalidOperationException("BCS auth response does not contain access_token.");
        }

        if (string.IsNullOrWhiteSpace(RefreshToken))
        {
            throw new InvalidOperationException("BCS auth response does not contain refresh_token.");
        }

        if (ExpiresIn <= 0)
        {
            throw new InvalidOperationException($"BCS auth response expires_in must be greater than zero. Actual value: {ExpiresIn}.");
        }

        if (RefreshExpiresIn <= 0)
        {
            throw new InvalidOperationException($"BCS auth response refresh_expires_in must be greater than zero. Actual value: {RefreshExpiresIn}.");
        }

        if (string.IsNullOrWhiteSpace(TokenType))
        {
            throw new InvalidOperationException("BCS auth response does not contain token_type.");
        }
    }
}

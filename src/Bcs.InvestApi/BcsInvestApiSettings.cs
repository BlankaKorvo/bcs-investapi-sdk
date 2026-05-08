namespace Bcs.InvestApi;

using Bcs.InvestApi.Auth;

public sealed class BcsInvestApiSettings
{
    public static readonly Uri DefaultAuthUrl = new("https://be.broker.ru/trade-api-keycloak/realms/tradeapi/protocol/openid-connect/token");

    /// <summary>
    /// Initial refresh token received in the BCS Investments web application.
    /// After the first refresh, the SDK uses the latest refresh_token from IBcsTokenStore.
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// BCS auth client id. Allowed values are trade-api-read and trade-api-write.
    /// </summary>
    public string ClientId { get; set; } = BcsAuthClientIds.TradeApiRead;

    /// <summary>
    /// Full Keycloak token endpoint URL.
    /// </summary>
    public Uri AuthUrl { get; set; } = DefaultAuthUrl;

    /// <summary>
    /// Optional HTTP timeout. If null, HttpClient default timeout is used.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Refresh access token before its actual expiration.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan TokenRefreshSkew { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Timer tick interval for automatic refresh. The timer checks whether refresh is required;
    /// it does not call the auth endpoint on every tick if the current access token is still valid.
    /// Default: 1 minute.
    /// </summary>
    public TimeSpan AutoRefreshInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Maximum time allowed to persist tokens after a successful auth refresh.
    /// User cancellation is intentionally ignored during this write so a rotated refresh token is not lost.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan TokenPersistenceTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Optional JSON file path for storing access_token and refresh_token.
    /// If null or empty, the factory/DI registration uses in-memory token storage.
    /// The file contains plaintext tokens; protect it with OS-level file permissions.
    /// </summary>
    public string? TokenStoragePath { get; set; }

    internal void ValidateTransportSettings()
    {
        if (AuthUrl is null)
        {
            throw new InvalidOperationException("BCS auth URL is not configured. Set Bcs:AuthUrl.");
        }

        if (!AuthUrl.IsAbsoluteUri || (AuthUrl.Scheme != Uri.UriSchemeHttps && AuthUrl.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException($"BCS auth URL must be an absolute HTTP/HTTPS URI. Actual value: '{AuthUrl}'.");
        }

        if (!BcsAuthClientIds.IsKnown(ClientId))
        {
            throw new InvalidOperationException($"BCS auth client_id must be '{BcsAuthClientIds.TradeApiRead}' or '{BcsAuthClientIds.TradeApiWrite}'. Actual value: '{ClientId}'.");
        }

        if (Timeout is not null && Timeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("BCS HTTP timeout must be greater than zero.");
        }
    }

    internal void ValidateTokenSettings()
    {
        ValidateTransportSettings();

        if (TokenRefreshSkew < TimeSpan.Zero)
        {
            throw new InvalidOperationException("BCS token refresh skew must be greater than or equal to zero.");
        }

        if (AutoRefreshInterval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("BCS auto-refresh interval must be greater than zero.");
        }

        if (TokenPersistenceTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("BCS token persistence timeout must be greater than zero.");
        }
    }

    internal string GetRequiredRefreshToken()
    {
        if (string.IsNullOrWhiteSpace(RefreshToken))
        {
            throw new InvalidOperationException("BCS refresh token is not configured. Set Bcs:RefreshToken, configure TokenStoragePath with a saved refresh token, or pass refresh token explicitly to Auth.GetAccessTokenAsync(...).");
        }

        return RefreshToken;
    }
}

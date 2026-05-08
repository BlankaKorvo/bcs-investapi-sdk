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
    /// Allows plain HTTP auth URLs for explicit local tests. Keep false in production.
    /// </summary>
    public bool AllowInsecureHttpForTesting { get; set; }

    /// <summary>
    /// Optional HTTP timeout. If null, HttpClient default timeout is used.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Maximum number of automatic retry attempts for auth refresh-token exchange after the initial request.
    /// Default: 0. Refresh tokens rotate, so automatic retries are disabled by default.
    /// </summary>
    public int AuthRetryAttempts { get; set; } = 0;

    /// <summary>
    /// Maximum number of HTTP retry attempts for idempotent read/query API requests after the initial request.
    /// Default: 3. Set to 0 to disable read/query retries.
    /// </summary>
    public int HttpRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay for exponential HTTP retry backoff.
    /// Default: 250 milliseconds.
    /// </summary>
    public TimeSpan HttpRetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(250);

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
    /// Maximum time allowed for one refresh-token auth exchange once refresh starts.
    /// Token persistence after successful auth uses TokenPersistenceTimeout.
    /// Default: 60 seconds.
    /// </summary>
    public TimeSpan TokenRefreshOperationTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Maximum time allowed to persist tokens after a successful auth refresh.
    /// User cancellation is intentionally ignored during this write so a rotated refresh token is not lost.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan TokenPersistenceTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum time allowed to acquire token storage lock during startup token source preflight.
    /// Default: 10 seconds.
    /// </summary>
    public TimeSpan TokenStoreLockTimeout { get; set; } = TimeSpan.FromSeconds(10);

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

        var isAllowedInsecureHttp = AuthUrl.Scheme == Uri.UriSchemeHttp && AllowInsecureHttpForTesting;
        if (!AuthUrl.IsAbsoluteUri || (AuthUrl.Scheme != Uri.UriSchemeHttps && !isAllowedInsecureHttp))
        {
            throw new InvalidOperationException($"BCS auth URL must be an absolute HTTPS URI. Actual value: '{AuthUrl}'.");
        }

        if (!BcsAuthClientIds.IsKnown(ClientId))
        {
            throw new InvalidOperationException($"BCS auth client_id must be '{BcsAuthClientIds.TradeApiRead}' or '{BcsAuthClientIds.TradeApiWrite}'. Actual value: '{ClientId}'.");
        }

        if (Timeout is not null && Timeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("BCS HTTP timeout must be greater than zero.");
        }

        if (AuthRetryAttempts < 0)
        {
            throw new InvalidOperationException("BCS auth retry attempts must be greater than or equal to zero.");
        }

        if (HttpRetryAttempts < 0)
        {
            throw new InvalidOperationException("BCS HTTP retry attempts must be greater than or equal to zero.");
        }

        if (HttpRetryBaseDelay < TimeSpan.Zero)
        {
            throw new InvalidOperationException("BCS HTTP retry base delay must be greater than or equal to zero.");
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

        if (TokenRefreshOperationTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("BCS token refresh operation timeout must be greater than zero.");
        }

        if (TokenPersistenceTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("BCS token persistence timeout must be greater than zero.");
        }

        if (TokenStoreLockTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("BCS token store lock timeout must be greater than zero.");
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

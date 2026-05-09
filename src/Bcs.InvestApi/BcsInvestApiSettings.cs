namespace Bcs.InvestApi;

using Bcs.InvestApi.Auth;

public sealed class BcsInvestApiSettings
{
    public static readonly Uri DefaultBaseUrl = new("https://be.broker.ru");

    public static readonly Uri DefaultAuthUrl = new("https://be.broker.ru/trade-api-keycloak/realms/tradeapi/protocol/openid-connect/token");

    /// <summary>
    /// Stable external bootstrap secret owned by the upper application layer.
    /// The SDK keeps access_token and rotated refresh_token values received from auth only in memory.
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
    /// Base URL for BCS HTTP API endpoints.
    /// </summary>
    public Uri BaseUrl { get; set; } = DefaultBaseUrl;

    /// <summary>
    /// Allows plain HTTP URLs for explicit local tests. Keep false in production.
    /// </summary>
    public bool AllowInsecureHttpForTesting { get; set; }

    /// <summary>
    /// Optional HTTP timeout. If null, HttpClient default timeout is used.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

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
    /// Default: 60 seconds.
    /// </summary>
    public TimeSpan TokenRefreshOperationTimeout { get; set; } = TimeSpan.FromSeconds(60);

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

        ValidateBaseUrl();

        if (Timeout is not null && Timeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("BCS HTTP timeout must be greater than zero.");
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
        _ = GetRequiredRefreshToken();

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
    }

    internal string GetRequiredRefreshToken()
    {
        if (string.IsNullOrWhiteSpace(RefreshToken))
        {
            throw new InvalidOperationException("BCS refresh token is not configured. Set Bcs:RefreshToken or pass refresh token explicitly.");
        }

        return RefreshToken;
    }

    internal Uri CreateEndpointUrl(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ValidateBaseUrl();

        return new Uri(EnsureTrailingSlash(BaseUrl), relativePath);
    }

    private void ValidateBaseUrl()
    {
        if (BaseUrl is null)
        {
            throw new InvalidOperationException("BCS base URL is not configured. Set Bcs:BaseUrl.");
        }

        var isAllowedInsecureHttp = BaseUrl.Scheme == Uri.UriSchemeHttp && AllowInsecureHttpForTesting;
        if (!BaseUrl.IsAbsoluteUri || (BaseUrl.Scheme != Uri.UriSchemeHttps && !isAllowedInsecureHttp))
        {
            throw new InvalidOperationException($"BCS base URL must be an absolute HTTPS URI. Actual value: '{BaseUrl}'.");
        }

        if (!string.IsNullOrEmpty(BaseUrl.Query) || !string.IsNullOrEmpty(BaseUrl.Fragment))
        {
            throw new InvalidOperationException($"BCS base URL must not contain query or fragment components. Actual value: '{BaseUrl}'.");
        }
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        if (uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal))
        {
            return uri;
        }

        return new Uri(uri.AbsoluteUri + "/");
    }
}

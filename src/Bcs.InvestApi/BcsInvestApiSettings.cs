namespace Bcs.InvestApi;

using Bcs.InvestApi.Contracts.Enums;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Tokens;

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
    /// BCS auth client id. Supported values map to trade-api-read and trade-api-write.
    /// </summary>
    public BcsAuthClientIds ClientId { get; set; } = BcsAuthClientIds.TradeApiWrite;

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
    /// Refresh access token before its actual expiration.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan TokenRefreshSkew { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum time allowed for one refresh-token auth exchange once refresh starts.
    /// Default: 60 seconds.
    /// </summary>
    public TimeSpan TokenRefreshOperationTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Interval between keep-alive pings the client sends on a WebSocket connection.
    /// .NET also answers the server's pings automatically while a receive is pending.
    /// BCS pings every 30 seconds; the default mirrors that cadence.
    /// Set to <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> to disable client keep-alive entirely.
    /// </summary>
    public TimeSpan WebSocketKeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long to wait for a pong before the WebSocket connection is treated as dead and aborted.
    /// Default: 20 seconds. Set to <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> to disable
    /// timeout-based abort (the client still pings, but a missing pong will not force a disconnect).
    /// </summary>
    public TimeSpan WebSocketKeepAliveTimeout { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Optional diagnostics callback invoked after every successful refresh-token exchange.
    /// Use it to log token rotation cadence and bootstrap-vs-rotated provenance. The payload carries
    /// no token values. Exceptions thrown by the callback are swallowed and never break the auth flow.
    /// </summary>
    public Action<BcsTokenRefreshInfo>? OnTokenRefreshed { get; set; }

    /// <summary>
    /// Optional diagnostics callback invoked when a refresh-token exchange fails
    /// (auth error, timeout, transport failure). Exceptions thrown by the callback are swallowed
    /// and never break the auth flow.
    /// </summary>
    public Action<BcsTokenRefreshFailure>? OnTokenRefreshFailed { get; set; }

    internal string GetRequiredRefreshToken()
    {
        ValidateRefreshToken();

        return RefreshToken!;
    }

    internal Uri CreateEndpointUrl(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ValidateBaseUrl();

        return new Uri(BcsUri.EnsureTrailingSlash(BaseUrl), relativePath);
    }

    internal void ValidateBaseUrl()
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

    internal void ValidateAuthUrl()
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
    }

    internal void ValidateAuthClientId()
    {
        _ = ClientId.ToApiValue();
    }

    internal void ValidateRefreshToken()
    {
        if (string.IsNullOrWhiteSpace(RefreshToken))
        {
            throw new InvalidOperationException("BCS refresh token is not configured. Set Bcs:RefreshToken or pass refresh token explicitly.");
        }
    }

    internal void ValidateTokenTimings()
    {
        if (TokenRefreshSkew < TimeSpan.Zero)
        {
            throw new InvalidOperationException("BCS token refresh skew must be greater than or equal to zero.");
        }

        if (TokenRefreshOperationTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("BCS token refresh operation timeout must be greater than zero.");
        }
    }

    internal void ValidateHttpTimeout()
    {
        if (Timeout is not null && Timeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("BCS HTTP timeout must be greater than zero.");
        }
    }

    internal void ValidateWebSocketKeepAlive()
    {
        if (WebSocketKeepAliveInterval <= TimeSpan.Zero &&
            WebSocketKeepAliveInterval != System.Threading.Timeout.InfiniteTimeSpan)
        {
            throw new InvalidOperationException(
                "BCS WebSocket keep-alive interval must be greater than zero or Timeout.InfiniteTimeSpan.");
        }

        if (WebSocketKeepAliveTimeout <= TimeSpan.Zero &&
            WebSocketKeepAliveTimeout != System.Threading.Timeout.InfiniteTimeSpan)
        {
            throw new InvalidOperationException(
                "BCS WebSocket keep-alive timeout must be greater than zero or Timeout.InfiniteTimeSpan.");
        }
    }

}

namespace Bcs.InvestApi;

using Bcs.InvestApi.Auth;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Time;
using Bcs.InvestApi.Tokens;

internal static class BcsInvestApiClientComposition
{
    public const string AuthHttpClientName = "Bcs.InvestApi.Auth";

    public static IBcsTokenStore CreateTokenStore(BcsInvestApiSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.ValidateTokenSettings();

        return string.IsNullOrWhiteSpace(settings.TokenStoragePath)
            ? new BcsInMemoryTokenStore()
            : new BcsFileTokenStore(settings.TokenStoragePath);
    }

    public static void ConfigureAuthHttpClient(BcsInvestApiSettings settings, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClient);

        settings.ValidateTransportSettings();

        if (settings.Timeout is not null)
        {
            httpClient.Timeout = settings.Timeout.Value;
        }
    }

    public static BcsAuthService CreateAuthService(BcsInvestApiSettings settings, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClient);

        return CreateAuthService(settings, httpClient, CreateAuthRequestSender(settings));
    }

    public static BcsAuthService CreateAuthService(
        BcsInvestApiSettings settings,
        HttpClient httpClient,
        BcsAuthRequestSender requestSender)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(requestSender);

        return new BcsAuthService(httpClient, settings, requestSender);
    }

    public static BcsAuthService CreateAuthService(
        BcsInvestApiSettings settings,
        Func<HttpClient> httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        return CreateAuthService(settings, httpClientFactory, CreateAuthRequestSender(settings));
    }

    public static BcsAuthService CreateAuthService(
        BcsInvestApiSettings settings,
        Func<HttpClient> httpClientFactory,
        BcsAuthRequestSender requestSender)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(requestSender);

        return new BcsAuthService(httpClientFactory, settings, requestSender);
    }

    public static BcsAuthRequestSender CreateAuthRequestSender(BcsInvestApiSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.ValidateTransportSettings();

        return new BcsAuthRequestSender(settings);
    }

    public static BcsApiRequestSender CreateApiRequestSender(BcsInvestApiSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.ValidateTransportSettings();

        return new BcsApiRequestSender(settings);
    }

    public static BcsHttpRequestSender CreateHttpRequestSender(BcsInvestApiSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.ValidateTransportSettings();

        return new BcsHttpRequestSender(settings);
    }

    public static BcsTokenManager CreateTokenManager(
        BcsAuthService authService,
        IBcsTokenStore tokenStore,
        BcsInvestApiSettings settings,
        IBcsClock? clock,
        IBcsTokenRefreshCoordinator? tokenRefreshCoordinator)
    {
        ArgumentNullException.ThrowIfNull(authService);
        ArgumentNullException.ThrowIfNull(tokenStore);
        ArgumentNullException.ThrowIfNull(settings);

        settings.ValidateTokenSettings();

        return new BcsTokenManager(
            authService,
            tokenStore,
            settings,
            clock,
            tokenRefreshCoordinator);
    }
}

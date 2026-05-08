namespace Bcs.InvestApi;

using Bcs.InvestApi.Auth;
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

        return new BcsAuthService(httpClient, settings);
    }

    public static BcsAuthService CreateAuthService(
        BcsInvestApiSettings settings,
        Func<HttpClient> httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        return new BcsAuthService(httpClientFactory, settings);
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

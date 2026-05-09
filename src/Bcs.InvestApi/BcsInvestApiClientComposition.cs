namespace Bcs.InvestApi;

using Bcs.InvestApi.Auth;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Limits;
using Bcs.InvestApi.Portfolio;
using Bcs.InvestApi.Time;
using Bcs.InvestApi.Tokens;

internal static class BcsInvestApiClientComposition
{
    public const string AuthHttpClientName = "Bcs.InvestApi.Auth";

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
        IBcsAuthHttpSender requestSender)
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
        IBcsAuthHttpSender requestSender)
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

    public static IBcsReadHttpSender CreateReadHttpSender(BcsInvestApiSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.ValidateTransportSettings();

        return new BcsReadHttpSender(settings);
    }

    public static IBcsCommandHttpSender CreateCommandHttpSender(BcsInvestApiSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.ValidateTransportSettings();

        return new BcsCommandHttpSender(settings);
    }

    public static BcsLimitsService CreateLimitsService(
        BcsInvestApiSettings settings,
        HttpClient httpClient,
        IBcsAccessTokenProvider tokens,
        IBcsReadHttpSender requestSender)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(requestSender);

        return new BcsLimitsService(settings, httpClient, tokens, requestSender);
    }

    public static BcsLimitsService CreateLimitsService(
        BcsInvestApiSettings settings,
        Func<HttpClient> httpClientFactory,
        IBcsAccessTokenProvider tokens,
        IBcsReadHttpSender requestSender)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(requestSender);

        return new BcsLimitsService(settings, httpClientFactory, tokens, requestSender);
    }

    public static BcsPortfolioService CreatePortfolioService(
        BcsInvestApiSettings settings,
        HttpClient httpClient,
        IBcsAccessTokenProvider tokens,
        IBcsReadHttpSender requestSender)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(requestSender);

        return new BcsPortfolioService(settings, httpClient, tokens, requestSender);
    }

    public static BcsPortfolioService CreatePortfolioService(
        BcsInvestApiSettings settings,
        Func<HttpClient> httpClientFactory,
        IBcsAccessTokenProvider tokens,
        IBcsReadHttpSender requestSender)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(requestSender);

        return new BcsPortfolioService(settings, httpClientFactory, tokens, requestSender);
    }

    public static BcsTokenManager CreateTokenManager(
        BcsAuthService authService,
        BcsInvestApiSettings settings,
        IBcsClock? clock)
    {
        ArgumentNullException.ThrowIfNull(authService);
        ArgumentNullException.ThrowIfNull(settings);

        return new BcsTokenManager(
            authService,
            settings,
            clock);
    }
}

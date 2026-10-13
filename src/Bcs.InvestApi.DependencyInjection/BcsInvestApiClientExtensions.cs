namespace Bcs.InvestApi.DependencyInjection;

using Bcs.InvestApi;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Services;
using Bcs.InvestApi.Tokens;
using Bcs.InvestApi.WebSockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public static class BcsInvestApiClientExtensions
{
    private const string HttpClientName = "Bcs.InvestApi";
    private const string LoggerCategory = "Bcs.InvestApi";

    public static IServiceCollection AddBcsInvestApiClient(
        this IServiceCollection services,
        Action<BcsInvestApiSettings> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<BcsInvestApiSettings>().Configure(configure);
        AddClientServices(services);

        return services;
    }

    public static IServiceCollection AddBcsInvestApiClient(
        this IServiceCollection services,
        Action<IServiceProvider, BcsInvestApiSettings> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddSingleton<IConfigureOptions<BcsInvestApiSettings>>(sp =>
            new ConfigureNamedOptions<BcsInvestApiSettings>(
                Options.DefaultName,
                settings => configure(sp, settings)));

        AddClientServices(services);

        return services;
    }

    private static void AddClientServices(IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddOptions<BcsInvestApiSettings>()
            .PostConfigure<ILoggerFactory>(AttachDiagnosticsLogging);

        services.AddSingleton<BcsHttpRequestSender>();
        services.AddSingleton<IBcsHttpSender>(sp => sp.GetRequiredService<BcsHttpRequestSender>());

        services.AddHttpClient(HttpClientName, (sp, httpClient) =>
        {
            var settings = sp.GetRequiredService<IOptions<BcsInvestApiSettings>>().Value;
            BcsInvestApiHttpClientConfiguration.ConfigureHttpClient(settings, httpClient);
        });

        services.AddSingleton<BcsAuthService>(sp => BcsAuthService.ForPooledFactory(
            CreateHttpClientFactory(sp),
            GetSettings(sp),
            sp.GetRequiredService<IBcsHttpSender>()));

        services.AddSingleton<BcsTokenManager>(sp => new BcsTokenManager(
            sp.GetRequiredService<BcsAuthService>(),
            GetSettings(sp),
            sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IBcsAccessTokenProvider>(sp => sp.GetRequiredService<BcsTokenManager>());

        AddEndpoint(services, (settings, createHttpClient, tokens, sender) =>
            new BcsLimitsService(settings, createHttpClient, tokens, sender));
        AddEndpoint(services, (settings, createHttpClient, tokens, sender) =>
            new BcsPortfolioService(settings, createHttpClient, tokens, sender));
        AddEndpoint(services, (settings, createHttpClient, tokens, sender) =>
            new BcsTradingScheduleService(settings, createHttpClient, tokens, sender));
        AddEndpoint(services, (settings, createHttpClient, tokens, sender) =>
            new BcsInstrumentsService(settings, createHttpClient, tokens, sender));
        AddEndpoint(services, (settings, createHttpClient, tokens, sender) =>
            new BcsMarketDataService(settings, createHttpClient, tokens, sender));
        AddEndpoint(services, (settings, createHttpClient, tokens, sender) =>
            new BcsOrdersService(settings, createHttpClient, tokens, sender));
        AddEndpoint(services, (settings, createHttpClient, tokens, sender) =>
            new BcsTradesService(settings, createHttpClient, tokens, sender));
        AddEndpoint(services, (settings, createHttpClient, tokens, sender) =>
            new BcsMarginalIndicatorsService(settings, createHttpClient, tokens, sender));

        services.AddSingleton(sp =>
        {
            var settings = GetSettings(sp);
            settings.ValidateWebSocketKeepAlive();
            return new BcsWebSocketConnector(
                sp.GetRequiredService<IBcsAccessTokenProvider>(),
                new BcsClientWebSocketTransport(
                    settings.WebSocketKeepAliveInterval,
                    settings.WebSocketKeepAliveTimeout));
        });

        services.AddSingleton(sp => new BcsInvestApiClient(
            sp.GetRequiredService<BcsTokenManager>(),
            sp.GetRequiredService<BcsLimitsService>(),
            sp.GetRequiredService<BcsPortfolioService>(),
            sp.GetRequiredService<BcsTradingScheduleService>(),
            sp.GetRequiredService<BcsInstrumentsService>(),
            sp.GetRequiredService<BcsMarketDataService>(),
            sp.GetRequiredService<BcsOrdersService>(),
            sp.GetRequiredService<BcsTradesService>(),
            sp.GetRequiredService<BcsMarginalIndicatorsService>(),
            sp.GetRequiredService<BcsWebSocketConnector>()));
    }

    private static void AddEndpoint<TService>(
        IServiceCollection services,
        Func<BcsInvestApiSettings, Func<HttpClient>, IBcsAccessTokenProvider, IBcsHttpSender, TService> createService)
        where TService : class
    {
        services.AddSingleton(sp => createService(
            GetSettings(sp),
            CreateHttpClientFactory(sp),
            sp.GetRequiredService<IBcsAccessTokenProvider>(),
            sp.GetRequiredService<IBcsHttpSender>()));
    }

    private static void AttachDiagnosticsLogging(BcsInvestApiSettings settings, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(LoggerCategory);

        var userOnRefreshed = settings.OnTokenRefreshed;
        settings.OnTokenRefreshed = info =>
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "BCS access token refreshed (source: {Source}). Access expires at {AccessExpiresAtUtc:O}, refresh expires at {RefreshExpiresAtUtc:O}.",
                    info.Source,
                    info.AccessTokenExpiresAtUtc,
                    info.RefreshTokenExpiresAtUtc);
            }

            userOnRefreshed?.Invoke(info);
        };

        var userOnFailed = settings.OnTokenRefreshFailed;
        settings.OnTokenRefreshFailed = failure =>
        {
            logger.LogError(
                failure.Exception,
                "BCS token refresh failed (source: {Source}).",
                failure.Source);

            userOnFailed?.Invoke(failure);
        };
    }

    private static BcsInvestApiSettings GetSettings(IServiceProvider sp) =>
        sp.GetRequiredService<IOptions<BcsInvestApiSettings>>().Value;

    private static Func<HttpClient> CreateHttpClientFactory(IServiceProvider sp)
    {
        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        return () => httpClientFactory.CreateClient(HttpClientName);
    }
}

namespace Bcs.InvestApi;

using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Services;
using Bcs.InvestApi.Tokens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

public static class BcsInvestApiClientExtensions
{
    private const string AuthHttpClientName = "Bcs.InvestApi.Auth";

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

        services.AddSingleton<BcsHttpRequestSender>();
        services.AddSingleton<IBcsHttpSender>(sp => sp.GetRequiredService<BcsHttpRequestSender>());

        services.AddHttpClient(AuthHttpClientName, (sp, httpClient) =>
        {
            var settings = sp.GetRequiredService<IOptions<BcsInvestApiSettings>>().Value;
            BcsInvestApiHttpClientConfiguration.ConfigureAuthHttpClient(settings, httpClient);
        });

        services.AddSingleton<BcsAuthService>(sp => new BcsAuthService(
            CreateAuthHttpClientFactory(sp),
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

        services.AddSingleton(sp => new BcsInvestApiClient(
            sp.GetRequiredService<BcsTokenManager>(),
            sp.GetRequiredService<BcsLimitsService>(),
            sp.GetRequiredService<BcsPortfolioService>(),
            sp.GetRequiredService<BcsTradingScheduleService>(),
            sp.GetRequiredService<BcsInstrumentsService>(),
            sp.GetRequiredService<BcsMarketDataService>()));
    }

    private static void AddEndpoint<TService>(
        IServiceCollection services,
        Func<BcsInvestApiSettings, Func<HttpClient>, IBcsAccessTokenProvider, IBcsHttpSender, TService> createService)
        where TService : class
    {
        services.AddSingleton(sp => createService(
            GetSettings(sp),
            CreateAuthHttpClientFactory(sp),
            sp.GetRequiredService<IBcsAccessTokenProvider>(),
            sp.GetRequiredService<IBcsHttpSender>()));
    }

    private static BcsInvestApiSettings GetSettings(IServiceProvider sp) =>
        sp.GetRequiredService<IOptions<BcsInvestApiSettings>>().Value;

    private static Func<HttpClient> CreateAuthHttpClientFactory(IServiceProvider sp)
    {
        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        return () => httpClientFactory.CreateClient(AuthHttpClientName);
    }
}

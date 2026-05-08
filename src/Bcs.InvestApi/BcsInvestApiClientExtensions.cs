namespace Bcs.InvestApi;

using Bcs.InvestApi.Auth;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Time;
using Bcs.InvestApi.Tokens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public static class BcsInvestApiClientExtensions
{
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
        services.AddSingleton<IBcsClock, BcsSystemClock>();

        services.AddSingleton<BcsAuthRequestSender>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<BcsInvestApiSettings>>().Value;
            return BcsInvestApiClientComposition.CreateAuthRequestSender(settings);
        });
        services.AddSingleton<IBcsAuthHttpSender>(sp => sp.GetRequiredService<BcsAuthRequestSender>());

        services.AddSingleton<IBcsReadHttpSender>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<BcsInvestApiSettings>>().Value;
            return BcsInvestApiClientComposition.CreateReadHttpSender(settings);
        });

        services.AddSingleton<IBcsCommandHttpSender>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<BcsInvestApiSettings>>().Value;
            return BcsInvestApiClientComposition.CreateCommandHttpSender(settings);
        });

        services.AddSingleton<IBcsTokenStore>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<BcsInvestApiSettings>>().Value;
            return BcsInvestApiClientComposition.CreateTokenStore(settings);
        });

        services.AddHttpClient(BcsInvestApiClientComposition.AuthHttpClientName, (sp, httpClient) =>
        {
            var settings = sp.GetRequiredService<IOptions<BcsInvestApiSettings>>().Value;
            BcsInvestApiClientComposition.ConfigureAuthHttpClient(settings, httpClient);
        });

        services.AddSingleton<BcsAuthService>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<BcsInvestApiSettings>>().Value;
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();

            return BcsInvestApiClientComposition.CreateAuthService(
                settings,
                () => httpClientFactory.CreateClient(BcsInvestApiClientComposition.AuthHttpClientName),
                sp.GetRequiredService<IBcsAuthHttpSender>());
        });

        services.AddSingleton<BcsTokenManager>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<BcsInvestApiSettings>>().Value;
            var tokenStore = sp.GetRequiredService<IBcsTokenStore>();

            return BcsInvestApiClientComposition.CreateTokenManager(
                sp.GetRequiredService<BcsAuthService>(),
                tokenStore,
                settings,
                sp.GetRequiredService<IBcsClock>(),
                sp.GetService<IBcsTokenRefreshCoordinator>());
        });
        services.AddSingleton<IBcsAccessTokenProvider>(sp => sp.GetRequiredService<BcsTokenManager>());
        services.AddSingleton<BcsInvestApiClient>();
    }
}

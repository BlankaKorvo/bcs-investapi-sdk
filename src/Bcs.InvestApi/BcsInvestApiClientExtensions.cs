namespace Bcs.InvestApi;

using Bcs.InvestApi.Auth;
using Bcs.InvestApi.Time;
using Bcs.InvestApi.Tokens;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddSingleton<IBcsClock, BcsSystemClock>();

        services.AddSingleton<IBcsTokenStore>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<BcsInvestApiSettings>>().Value;
            settings.ValidateTokenSettings();

            return string.IsNullOrWhiteSpace(settings.TokenStoragePath)
                ? new BcsInMemoryTokenStore()
                : new BcsFileTokenStore(settings.TokenStoragePath);
        });

        services.AddHttpClient(AuthHttpClientName, (sp, httpClient) =>
        {
            var settings = sp.GetRequiredService<IOptions<BcsInvestApiSettings>>().Value;
            settings.ValidateTransportSettings();

            if (settings.Timeout is not null)
            {
                httpClient.Timeout = settings.Timeout.Value;
            }
        });

        services.AddSingleton<BcsAuthService>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<BcsInvestApiSettings>>().Value;
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();

            return new BcsAuthService(
                () => httpClientFactory.CreateClient(AuthHttpClientName),
                settings);
        });

        services.AddSingleton<BcsTokenManager>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<BcsInvestApiSettings>>().Value;
            settings.ValidateTokenSettings();

            var tokenStore = sp.GetRequiredService<IBcsTokenStore>();

            return new BcsTokenManager(
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

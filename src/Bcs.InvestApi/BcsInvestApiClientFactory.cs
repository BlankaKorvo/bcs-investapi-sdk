namespace Bcs.InvestApi;

using Bcs.InvestApi.DTO.Enums;
using Bcs.InvestApi.Services;

public static class BcsInvestApiClientFactory
{
    public static BcsInvestApiClient Create(
        string refreshToken,
        BcsAuthClientIds clientId = BcsAuthClientIds.TradeApiRead,
        HttpMessageHandler? httpMessageHandler = null)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ArgumentException("BCS refresh token is required.", nameof(refreshToken));
        }

        return Create(
            new BcsInvestApiSettings
            {
                RefreshToken = refreshToken,
                ClientId = clientId,
            },
            httpMessageHandler);
    }

    public static BcsInvestApiClient Create(
        BcsInvestApiSettings settings,
        HttpMessageHandler? httpMessageHandler = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.ValidateTokenSettings();

        var httpClient = httpMessageHandler is null
            ? new HttpClient()
            : new HttpClient(httpMessageHandler, disposeHandler: false);

        BcsInvestApiHttpClientConfiguration.ConfigureAuthHttpClient(settings, httpClient);

        var services = BcsInvestApiClientServices.Create(
            settings,
            httpClient,
            timeProvider);

        return services.CreateClient(
            ownsTokenManager: true,
            ownedTransport: httpClient);
    }
}

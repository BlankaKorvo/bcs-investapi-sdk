namespace Bcs.InvestApi;

internal static class BcsInvestApiHttpClientConfiguration
{
    internal static void ConfigureAuthHttpClient(BcsInvestApiSettings settings, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClient);

        settings.ValidateTransportSettings();

        if (settings.Timeout is not null)
        {
            httpClient.Timeout = settings.Timeout.Value;
        }
    }
}

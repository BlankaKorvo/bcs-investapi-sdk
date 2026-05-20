namespace Bcs.InvestApi;

internal static class BcsInvestApiHttpClientConfiguration
{
    internal static void ConfigureHttpClient(BcsInvestApiSettings settings, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClient);

        settings.ValidateHttpTimeout();

        if (settings.Timeout is not null)
        {
            httpClient.Timeout = settings.Timeout.Value;
        }
    }
}

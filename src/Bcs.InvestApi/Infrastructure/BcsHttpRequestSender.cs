namespace Bcs.InvestApi.Infrastructure;

internal sealed class BcsHttpRequestSender : IBcsHttpSender
{
    public Task<BcsHttpExchange> SendAsync(
        HttpClient httpClient,
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(requestFactory);

        return SendOnceAsync(httpClient, requestFactory, cancellationToken);
    }

    private static async Task<BcsHttpExchange> SendOnceAsync(
        HttpClient httpClient,
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        var request = requestFactory();
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var response = await httpClient
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            return new BcsHttpExchange(request, response);
        }
        catch
        {
            request.Dispose();
            throw;
        }
    }
}

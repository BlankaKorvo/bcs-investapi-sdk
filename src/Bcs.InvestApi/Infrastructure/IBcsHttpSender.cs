namespace Bcs.InvestApi.Infrastructure;

internal interface IBcsHttpSender
{
    Task<BcsHttpExchange> SendAsync(
        HttpClient httpClient,
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken);
}

namespace Bcs.InvestApi.Infrastructure;

internal sealed class BcsApiRequestSender
{
    private readonly BcsHttpRequestSender _sender;

    public BcsApiRequestSender(BcsInvestApiSettings settings)
        : this(new BcsHttpRequestSender(BcsHttpRetryPolicy.CreateForApi(settings)))
    {
    }

    internal BcsApiRequestSender(BcsHttpRequestSender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    public Task<BcsHttpExchange> SendAsync(
        HttpClient httpClient,
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken) =>
        _sender.SendAsync(httpClient, requestFactory, cancellationToken);
}

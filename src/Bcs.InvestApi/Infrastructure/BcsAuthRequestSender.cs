namespace Bcs.InvestApi.Infrastructure;

internal sealed class BcsAuthRequestSender
{
    private readonly BcsHttpRequestSender _sender;

    public BcsAuthRequestSender(BcsInvestApiSettings settings)
        : this(new BcsHttpRequestSender(BcsHttpRetryPolicy.CreateForAuth(settings)))
    {
    }

    internal BcsAuthRequestSender(BcsHttpRequestSender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    public Task<BcsHttpExchange> SendAsync(
        HttpClient httpClient,
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken) =>
        _sender.SendAsync(httpClient, requestFactory, cancellationToken);
}

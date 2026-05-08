namespace Bcs.InvestApi.Infrastructure;

internal sealed class BcsCommandHttpSender : IBcsCommandHttpSender
{
    private readonly BcsHttpRequestSender _sender;

    public BcsCommandHttpSender(BcsInvestApiSettings settings)
        : this(new BcsHttpRequestSender(BcsHttpRetryPolicy.CreateForCommand(settings)))
    {
    }

    internal BcsCommandHttpSender(BcsHttpRequestSender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    public Task<BcsHttpExchange> SendAsync(
        HttpClient httpClient,
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken) =>
        _sender.SendAsync(httpClient, requestFactory, cancellationToken);
}

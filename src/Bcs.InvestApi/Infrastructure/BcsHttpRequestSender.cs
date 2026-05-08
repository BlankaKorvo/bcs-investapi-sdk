namespace Bcs.InvestApi.Infrastructure;

using Polly;

internal sealed class BcsHttpRequestSender
{
    private readonly IAsyncPolicy<BcsHttpExchange> _retryPolicy;

    public BcsHttpRequestSender(BcsInvestApiSettings settings)
        : this(BcsHttpRetryPolicy.Create(settings))
    {
    }

    internal BcsHttpRequestSender(IAsyncPolicy<BcsHttpExchange> retryPolicy)
    {
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
    }

    public Task<BcsHttpExchange> SendAsync(
        HttpClient httpClient,
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(requestFactory);

        return _retryPolicy.ExecuteAsync(
            ct => SendOnceAsync(httpClient, requestFactory, ct),
            cancellationToken);
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

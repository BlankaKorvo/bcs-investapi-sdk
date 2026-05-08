namespace Bcs.InvestApi.Tests.Infrastructure;

using System.Net;
using Bcs.InvestApi.Infrastructure;
using Xunit;

public sealed class BcsHttpSenderRetryTests
{
    [Fact]
    public async Task ReadSender_TransientGet_RetriesWithFreshRequest()
    {
        var attempt = 0;
        var observedRequests = new List<HttpRequestMessage>();
        var handler = new CapturingHttpMessageHandler((request, _) =>
        {
            observedRequests.Add(request);

            return Task.FromResult(++attempt < 2
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : new HttpResponseMessage(HttpStatusCode.OK));
        });
        var sender = new BcsReadHttpSender(CreateSettings());

        using var exchange = await sender.SendAsync(
            new HttpClient(handler),
            () => new HttpRequestMessage(HttpMethod.Get, "https://example.test/limits"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, exchange.Response.StatusCode);
        Assert.Equal(2, handler.RequestCount);
        Assert.Equal(2, observedRequests.Distinct().Count());
    }

    [Fact]
    public async Task ReadSender_TransientQueryPost_RetriesWithFreshRequest()
    {
        var attempt = 0;
        var observedRequests = new List<HttpRequestMessage>();
        var handler = new CapturingHttpMessageHandler((request, _) =>
        {
            observedRequests.Add(request);

            return Task.FromResult(++attempt < 2
                ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                : new HttpResponseMessage(HttpStatusCode.OK));
        });
        var sender = new BcsHttpRequestSender(BcsHttpRetryPolicy.CreateForIdempotentQueryPost(CreateSettings()));

        using var exchange = await sender.SendAsync(
            new HttpClient(handler),
            () => new HttpRequestMessage(HttpMethod.Post, "https://example.test/instruments/by-tickers")
            {
                Content = new StringContent("""{"tickers":["SBER"]}"""),
            },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, exchange.Response.StatusCode);
        Assert.Equal(2, handler.RequestCount);
        Assert.Equal(2, observedRequests.Distinct().Count());
    }

    [Fact]
    public async Task CommandSender_TransientStatus_DoesNotRetry()
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        var sender = new BcsCommandHttpSender(CreateSettings());

        using var exchange = await sender.SendAsync(
            new HttpClient(handler),
            () => new HttpRequestMessage(HttpMethod.Post, "https://example.test/orders"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.InternalServerError, exchange.Response.StatusCode);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CommandSender_TransientException_DoesNotRetry()
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            throw new HttpRequestException("Connection reset after order request."));
        var sender = new BcsCommandHttpSender(CreateSettings());

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            sender.SendAsync(
                new HttpClient(handler),
                () => new HttpRequestMessage(HttpMethod.Post, "https://example.test/orders"),
                CancellationToken.None));

        Assert.Contains("Connection reset", exception.Message);
        Assert.Equal(1, handler.RequestCount);
    }

    private static BcsInvestApiSettings CreateSettings() =>
        new()
        {
            AuthUrl = new Uri("https://example.test/token"),
            HttpRetryAttempts = 2,
            HttpRetryBaseDelay = TimeSpan.Zero,
        };
}

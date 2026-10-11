namespace Bcs.InvestApi.Tests.MarginalIndicators;

using System.Net;
using Bcs.InvestApi;
using Bcs.InvestApi.Contracts.Errors;
using Bcs.InvestApi.Contracts.Exceptions;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Services;
using Bcs.InvestApi.Tests.Infrastructure;
using Bcs.InvestApi.Tokens;
using Xunit;

public sealed class BcsMarginalIndicatorsServiceTests
{
    [Fact]
    public async Task GetInstrumentsDiscountsAsync_PerformsGetRequestAndDeserializesResponse()
    {
        const string discountsJson = """
        [
          {
            "ticker": "SBER",
            "discountLong": 0.25,
            "discountShort": 0.40
          },
          {
            "ticker": "GAZP",
            "discountLong": 0.50,
            "discountShort": 0.65
          }
        ]
        """;

        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, discountsJson)));
        var service = new BcsMarginalIndicatorsService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var discounts = await service.GetInstrumentsDiscountsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal(
            new Uri("https://example.test/trade-api-bff-marginal-indicators/api/v1/instruments-discounts"),
            handler.LastRequest?.RequestUri);
        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("access-token-1", handler.LastRequest?.Headers.Authorization?.Parameter);
        Assert.Null(handler.LastRequestContent);

        Assert.Equal(2, discounts.Count);
        Assert.Equal("SBER", discounts[0].Ticker);
        Assert.Equal(0.25m, discounts[0].DiscountLong);
        Assert.Equal(0.40m, discounts[0].DiscountShort);
        Assert.Equal("GAZP", discounts[1].Ticker);
        Assert.Equal(0.50m, discounts[1].DiscountLong);
        Assert.Equal(0.65m, discounts[1].DiscountShort);
    }

    [Fact]
    public async Task GetInstrumentsDiscountsAsync_EmptyArray_ReturnsEmptyCollection()
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, "[]")));
        var service = new BcsMarginalIndicatorsService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var discounts = await service.GetInstrumentsDiscountsAsync(TestContext.Current.CancellationToken);

        Assert.Empty(discounts);
    }

    [Fact]
    public async Task GetInstrumentsDiscountsAsync_TooManyRequests_ThrowsWithApiError()
    {
        const string errorJson = """
        {
          "timestamp": 1715000000,
          "traceId": "trace-discounts-1",
          "type": "RESOURCE_EXHAUSTED",
          "errors": [],
          "displayOptions": {}
        }
        """;

        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.TooManyRequests, errorJson)));
        var service = new BcsMarginalIndicatorsService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var exception = await Assert.ThrowsAsync<BcsApiException>(() =>
            service.GetInstrumentsDiscountsAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
        Assert.Equal("instruments-discounts", exception.Endpoint);
        Assert.NotNull(exception.ApiError);
        Assert.Equal(BcsApiErrorTypes.ResourceExhausted, exception.ApiError!.Type);
        Assert.Equal("trace-discounts-1", exception.ApiError.TraceId);
    }

    private static BcsInvestApiSettings CreateSettings() =>
        new()
        {
            BaseUrl = new Uri("https://example.test"),
        };

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };

    private sealed class StaticTokenProvider : IBcsAccessTokenProvider
    {
        private readonly string _accessToken;

        public StaticTokenProvider(string accessToken)
        {
            _accessToken = accessToken;
        }

        public ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_accessToken);

        public void InvalidateAccessToken(string rejectedAccessToken)
        {
        }
    }
}

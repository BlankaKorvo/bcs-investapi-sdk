namespace Bcs.InvestApi.Tests.Trades;

using System.Net;
using Bcs.InvestApi;
using Bcs.InvestApi.Contracts.Enums;
using Bcs.InvestApi.Contracts.Errors;
using Bcs.InvestApi.Contracts.Exceptions;
using Bcs.InvestApi.Contracts.Trades;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Services;
using Bcs.InvestApi.Tests.Infrastructure;
using Bcs.InvestApi.Tokens;
using Xunit;

public sealed class BcsTradesServiceTests
{
    [Fact]
    public async Task SearchTradesAsync_PostsJsonBodyAndDeserializesResponse()
    {
        const string tradesJson = """
        {
          "records": [
            {
              "orderNum": 4123,
              "ticker": "SBER",
              "tradeNum": 9876543,
              "clientCode": "2230377",
              "classCode": "TQBR",
              "settlementCurrency": "RUB",
              "baseCurrency": "RUB",
              "priceCurrency": "RUB",
              "side": "1",
              "instrumentType": "STOCK",
              "dealType": 0,
              "tradeDateTime": "2026-02-04T10:10:54.530Z",
              "price": 305.25,
              "volume": 30525,
              "go": 0,
              "contractAmount": 30525,
              "settleDate": "2026-02-05",
              "tradeQuantity": 100,
              "tradeQuantityLots": 10
            }
          ],
          "totalRecords": 1,
          "totalPages": 1
        }
        """;

        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, tradesJson)));
        var service = new BcsTradesService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var response = await service.SearchTradesAsync(
            new BcsTradesSearchRequest
            {
                Side = BcsOrderSide.Buy,
                Tickers = new[] { "SBER" },
                ClassCodes = new[] { "TQBR" },
                StartDateTime = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
                EndDateTime = new DateTimeOffset(2026, 2, 5, 0, 0, 0, TimeSpan.Zero),
            },
            page: 0,
            size: 50,
            sort: new[] { BcsTradeSort.TradeDateTimeDesc },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal(
            new Uri("https://example.test/trade-api-bff-trade-details/api/v1/trades/search?page=0&size=50&sort=tradeDateTime%2Cdesc"),
            handler.LastRequest?.RequestUri);
        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("access-token-1", handler.LastRequest?.Headers.Authorization?.Parameter);
        Assert.Equal("application/json", handler.LastRequest?.Content?.Headers.ContentType?.MediaType);
        Assert.Equal(
            """{"side":"1","tickers":["SBER"],"classCodes":["TQBR"],"startDateTime":"2026-02-01T00:00:00.000Z","endDateTime":"2026-02-05T00:00:00.000Z"}""",
            handler.LastRequestContent);

        Assert.Equal(1, response.TotalRecords);
        Assert.Equal(1, response.TotalPages);

        var trade = Assert.Single(response.Records);
        Assert.Equal(4123, trade.OrderNum);
        Assert.Equal("SBER", trade.Ticker);
        Assert.Equal(9876543, trade.TradeNum);
        Assert.Equal("2230377", trade.ClientCode);
        Assert.Equal("TQBR", trade.ClassCode);
        Assert.Equal("RUB", trade.SettlementCurrency);
        Assert.Equal("RUB", trade.BaseCurrency);
        Assert.Equal("RUB", trade.PriceCurrency);
        Assert.Equal("1", trade.Side);
        Assert.Equal("STOCK", trade.InstrumentType);
        Assert.Equal(0, trade.DealType);
        Assert.Equal(new DateTimeOffset(2026, 2, 4, 10, 10, 54, 530, TimeSpan.Zero), trade.TradeDateTime);
        Assert.Equal(305.25m, trade.Price);
        Assert.Equal(30525m, trade.Volume);
        Assert.Equal(0m, trade.Go);
        Assert.Equal(30525m, trade.ContractAmount);
        Assert.Equal(new DateOnly(2026, 2, 5), trade.SettleDate);
        Assert.Equal(100m, trade.TradeQuantity);
        Assert.Equal(10m, trade.TradeQuantityLots);
    }

    [Fact]
    public async Task SearchTradesAsync_WithEmptyFilters_SendsEmptyBody()
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, """{"records":[],"totalRecords":0,"totalPages":0}""")));
        var service = new BcsTradesService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        await service.SearchTradesAsync(
            new BcsTradesSearchRequest(),
            page: 0,
            size: 25,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("{}", handler.LastRequestContent);
        Assert.Equal(
            new Uri("https://example.test/trade-api-bff-trade-details/api/v1/trades/search?page=0&size=25"),
            handler.LastRequest?.RequestUri);
    }

    [Fact]
    public async Task SearchTradesAsync_WithMultipleSortValues_AppendsAllAsQueryParameters()
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, """{"records":[],"totalRecords":0,"totalPages":0}""")));
        var service = new BcsTradesService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        await service.SearchTradesAsync(
            new BcsTradesSearchRequest(),
            page: 1,
            size: 10,
            sort: new[] { BcsTradeSort.TickerAsc, BcsTradeSort.TradeDateTimeDesc },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            new Uri("https://example.test/trade-api-bff-trade-details/api/v1/trades/search?page=1&size=10&sort=ticker%2Casc&sort=tradeDateTime%2Cdesc"),
            handler.LastRequest?.RequestUri);
    }

    [Fact]
    public async Task SearchTradesAsync_WithTradeNumsFilter_SerializesAsInt64Array()
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, """{"records":[],"totalRecords":0,"totalPages":0}""")));
        var service = new BcsTradesService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        await service.SearchTradesAsync(
            new BcsTradesSearchRequest
            {
                TradeNums = new[] { 1L, 9876543210L },
            },
            page: 0,
            size: 50,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            """{"tradeNums":[1,9876543210]}""",
            handler.LastRequestContent);
    }

    [Theory]
    [InlineData(-1)]
    public async Task SearchTradesAsync_WithInvalidPage_Throws(int page)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SearchTradesAsync(new BcsTradesSearchRequest(), page: page, size: 50, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SearchTradesAsync_WithInvalidSize_Throws(int size)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SearchTradesAsync(new BcsTradesSearchRequest(), page: 0, size: size, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task SearchTradesAsync_WithNullOrWhitespaceTicker_Throws(string? ticker)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SearchTradesAsync(
                new BcsTradesSearchRequest { Tickers = new[] { ticker! } },
                page: 0,
                size: 50,
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task SearchTradesAsync_WithNullOrWhitespaceClassCode_Throws(string? classCode)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SearchTradesAsync(
                new BcsTradesSearchRequest { ClassCodes = new[] { classCode! } },
                page: 0,
                size: 50,
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchTradesAsync_TooManyRequests_ThrowsWithApiError()
    {
        const string errorJson = """
        {
          "timestamp": 1715000000,
          "traceId": "trace-trades-1",
          "type": "RESOURCE_EXHAUSTED",
          "errors": [],
          "displayOptions": {}
        }
        """;

        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.TooManyRequests, errorJson)));
        var service = new BcsTradesService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var exception = await Assert.ThrowsAsync<BcsApiException>(() =>
            service.SearchTradesAsync(new BcsTradesSearchRequest(), page: 0, size: 50, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
        Assert.Equal("trades-search", exception.Endpoint);
        Assert.NotNull(exception.ApiError);
        Assert.Equal(BcsApiErrorTypes.ResourceExhausted, exception.ApiError!.Type);
        Assert.Equal("trace-trades-1", exception.ApiError.TraceId);
    }

    private static BcsTradesService CreateService() =>
        new(
            CreateSettings(),
            new HttpClient(new CapturingHttpMessageHandler((_, _) =>
                Task.FromResult(JsonResponse(HttpStatusCode.OK, """{"records":[],"totalRecords":0,"totalPages":0}""")))),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

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

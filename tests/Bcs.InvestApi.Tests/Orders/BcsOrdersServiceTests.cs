namespace Bcs.InvestApi.Tests.Orders;

using System.Net;
using Bcs.InvestApi;
using Bcs.InvestApi.Contracts.Enums;
using Bcs.InvestApi.Contracts.Orders;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Services;
using Bcs.InvestApi.Tests.Infrastructure;
using Bcs.InvestApi.Tokens;
using Xunit;

public sealed class BcsOrdersServiceTests
{
    [Fact]
    public async Task SearchOrdersAsync_PostsJsonBodyAndDeserializesResponse()
    {
        const string ordersJson = """
        {
          "records": [
            {
              "orderNum": 123456789,
              "orderId": "260729-TQBR-123456789",
              "clientCode": "1234567",
              "executionDateTime": "2024-07-29T15:52:28.071Z",
              "executedValue": 3012.5,
              "orderDateTime": "2024-07-29T15:51:28.071Z",
              "tradeDate": "2024-07-29",
              "updateDateTime": "2024-07-29T15:53:28.071Z",
              "ticker": "SBER",
              "classCode": "TQBR",
              "takePrice": 310,
              "stopPrice": 295,
              "price": 301.25,
              "settlementCurrency": "RUB",
              "orderQuantity": 10,
              "remainedQuantity": 0,
              "executedQuantity": 10,
              "rejectReason": "",
              "averagePrice": 301.25,
              "calculationVolume": 3012.5,
              "contractSum": 3012.5,
              "orderStatus": 2,
              "orderType": 2,
              "side": 1,
              "orderQuantityLots": 1,
              "remainedQuantityLots": 0,
              "executedQuantityLots": 1,
              "linkedOrder": "linked-1",
              "stopOrder": "stop-1",
              "visible": 5,
              "marketTakeProfit": 2,
              "marketStopLoss": 1,
              "positionPriceStop": 294,
              "positionPriceLimit": 296
            }
          ],
          "totalRecords": 1,
          "totalPages": 1
        }
        """;

        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, ordersJson)));
        var service = new BcsOrdersService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var orders = await service.SearchOrdersAsync(
            new BcsOrdersSearchRequest
            {
                StartDateTime = new DateTimeOffset(2024, 07, 29, 15, 51, 28, 071, TimeSpan.Zero),
                EndDateTime = new DateTimeOffset(2024, 07, 30, 15, 51, 28, 071, TimeSpan.Zero),
                Side = BcsOrderSide.Buy,
                OrderStatus = new[] { BcsOrderStatus.Executed, BcsOrderStatus.Active },
                OrderTypes = new[] { BcsOrderType.Limit, BcsOrderType.LimitForThirtyDays },
                Tickers = new[] { " SBER ", "GAZP" },
                ClassCodes = new[] { "TQBR" },
            },
            page: 2,
            size: 50,
            sort: new[] { BcsOrderSort.OrderDateTimeDesc, BcsOrderSort.TickerAsc });

        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal(
            new Uri("https://example.test/trade-api-bff-order-details/api/v1/orders/search?page=2&size=50&sort=orderDateTime%2Cdesc&sort=ticker%2Casc"),
            handler.LastRequest?.RequestUri);
        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("access-token-1", handler.LastRequest?.Headers.Authorization?.Parameter);
        Assert.Equal("application/json", handler.LastRequest?.Content?.Headers.ContentType?.MediaType);
        Assert.Equal(
            """{"startDateTime":"2024-07-29T15:51:28.071Z","endDateTime":"2024-07-30T15:51:28.071Z","side":1,"orderStatus":[2,3],"orderTypes":[2,10],"tickers":[" SBER ","GAZP"],"classCodes":["TQBR"]}""",
            handler.LastRequestContent);

        Assert.Equal(1, orders.TotalRecords);
        Assert.Equal(1, orders.TotalPages);

        var order = Assert.Single(orders.Records);
        Assert.Equal(123456789, order.OrderNum);
        Assert.Equal("260729-TQBR-123456789", order.OrderId);
        Assert.Equal("1234567", order.ClientCode);
        Assert.Equal(new DateTimeOffset(2024, 07, 29, 15, 52, 28, 071, TimeSpan.Zero), order.ExecutionDateTime);
        Assert.Equal(3012.5m, order.ExecutedValue);
        Assert.Equal(new DateTimeOffset(2024, 07, 29, 15, 51, 28, 071, TimeSpan.Zero), order.OrderDateTime);
        Assert.Equal(new DateOnly(2024, 07, 29), order.TradeDate);
        Assert.Equal(new DateTimeOffset(2024, 07, 29, 15, 53, 28, 071, TimeSpan.Zero), order.UpdateDateTime);
        Assert.Equal("SBER", order.Ticker);
        Assert.Equal("TQBR", order.ClassCode);
        Assert.Equal(310m, order.TakePrice);
        Assert.Equal(295m, order.StopPrice);
        Assert.Equal(301.25m, order.Price);
        Assert.Equal("RUB", order.SettlementCurrency);
        Assert.Equal(10m, order.OrderQuantity);
        Assert.Equal(0m, order.RemainedQuantity);
        Assert.Equal(10m, order.ExecutedQuantity);
        Assert.Equal("", order.RejectReason);
        Assert.Equal(301.25m, order.AveragePrice);
        Assert.Equal(3012.5m, order.CalculationVolume);
        Assert.Equal(3012.5m, order.ContractSum);
        Assert.Equal(BcsOrderStatus.Executed, order.OrderStatus);
        Assert.Equal(BcsOrderType.Limit, order.OrderType);
        Assert.Equal(BcsOrderSide.Buy, order.Side);
        Assert.Equal(1m, order.OrderQuantityLots);
        Assert.Equal(0m, order.RemainedQuantityLots);
        Assert.Equal(1m, order.ExecutedQuantityLots);
        Assert.Equal("linked-1", order.LinkedOrder);
        Assert.Equal("stop-1", order.StopOrder);
        Assert.Equal(5m, order.Visible);
        Assert.Equal(2, order.MarketTakeProfit);
        Assert.Equal(1, order.MarketStopLoss);
        Assert.Equal(294m, order.PositionPriceStop);
        Assert.Equal(296m, order.PositionPriceLimit);
    }

    [Fact]
    public async Task SearchOrdersAsync_WithEmptyFilter_PostsEmptyJsonObject()
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, """{"records":[],"totalRecords":0,"totalPages":0}""")));
        var service = new BcsOrdersService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var orders = await service.SearchOrdersAsync(new BcsOrdersSearchRequest(), page: 0, size: 50);

        Assert.Equal(
            new Uri("https://example.test/trade-api-bff-order-details/api/v1/orders/search?page=0&size=50"),
            handler.LastRequest?.RequestUri);
        Assert.Equal("{}", handler.LastRequestContent);
        Assert.Empty(orders.Records);
    }

    [Fact]
    public async Task SearchOrdersAsync_WithNegativePage_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SearchOrdersAsync(new BcsOrdersSearchRequest(), page: -1, size: 50));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SearchOrdersAsync_WithInvalidSize_Throws(int size)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SearchOrdersAsync(new BcsOrdersSearchRequest(), page: 0, size: size));
    }

    [Fact]
    public async Task SearchOrdersAsync_WithSizeGreaterThanOneHundred_SendsRequest()
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, """{"records":[],"totalRecords":0,"totalPages":0}""")));
        var service = new BcsOrdersService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        await service.SearchOrdersAsync(new BcsOrdersSearchRequest(), page: 0, size: 101);

        Assert.Equal(
            new Uri("https://example.test/trade-api-bff-order-details/api/v1/orders/search?page=0&size=101"),
            handler.LastRequest?.RequestUri);
    }

    [Fact]
    public async Task SearchOrdersAsync_WithNullRequest_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.SearchOrdersAsync(null!, page: 0, size: 50));
    }

    [Fact]
    public async Task SearchOrdersAsync_WithUnsupportedSide_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SearchOrdersAsync(
                new BcsOrdersSearchRequest { Side = (BcsOrderSide)999 },
                page: 0,
                size: 50));
    }

    [Fact]
    public async Task SearchOrdersAsync_WithUnsupportedOrderStatus_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SearchOrdersAsync(
                new BcsOrdersSearchRequest { OrderStatus = new[] { (BcsOrderStatus)999 } },
                page: 0,
                size: 50));
    }

    [Fact]
    public async Task SearchOrdersAsync_WithUnsupportedOrderType_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SearchOrdersAsync(
                new BcsOrdersSearchRequest { OrderTypes = new[] { (BcsOrderType)999 } },
                page: 0,
                size: 50));
    }

    [Fact]
    public async Task SearchOrdersAsync_WithUnsupportedSort_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SearchOrdersAsync(
                new BcsOrdersSearchRequest(),
                page: 0,
                size: 50,
                sort: new[] { (BcsOrderSort)999 }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task SearchOrdersAsync_WithNullOrWhitespaceTicker_Throws(string? ticker)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SearchOrdersAsync(
                new BcsOrdersSearchRequest { Tickers = new[] { "SBER", ticker! } },
                page: 0,
                size: 50));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task SearchOrdersAsync_WithNullOrWhitespaceClassCode_Throws(string? classCode)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SearchOrdersAsync(
                new BcsOrdersSearchRequest { ClassCodes = new[] { "TQBR", classCode! } },
                page: 0,
                size: 50));
    }

    private static BcsOrdersService CreateService() =>
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

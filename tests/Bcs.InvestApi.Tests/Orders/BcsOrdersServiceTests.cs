namespace Bcs.InvestApi.Tests.Orders;

using System.Net;
using Bcs.InvestApi;
using Bcs.InvestApi.Contracts.Enums;
using Bcs.InvestApi.Contracts.Errors;
using Bcs.InvestApi.Contracts.Exceptions;
using Bcs.InvestApi.Contracts.Orders;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Services;
using Bcs.InvestApi.Tests.Infrastructure;
using Bcs.InvestApi.Tokens;
using Xunit;

public sealed class BcsOrdersServiceTests
{
    [Fact]
    public async Task CreateOrderAsync_PostsJsonBodyAndDeserializesResponse()
    {
        const string createJson = """
        {
          "clientOrderId": "12345678-1234-1234-1234-123456789123",
          "status": "OK"
        }
        """;

        var clientOrderId = Guid.Parse("12345678-1234-1234-1234-123456789123");
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, createJson)));
        var service = new BcsOrdersService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var response = await service.CreateOrderAsync(
            new BcsCreateOrderRequest
            {
                ClientOrderId = clientOrderId,
                Side = BcsOrderSide.Buy,
                OrderType = BcsCreateOrderType.Limit,
                OrderQuantity = 10,
                Ticker = "APTK",
                ClassCode = "TQBR",
                Price = 9.530m,
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal(
            new Uri("https://example.test/trade-api-bff-operations/api/v1/orders"),
            handler.LastRequest?.RequestUri);
        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("access-token-1", handler.LastRequest?.Headers.Authorization?.Parameter);
        Assert.Equal("application/json", handler.LastRequest?.Content?.Headers.ContentType?.MediaType);
        Assert.Equal(
            """{"clientOrderId":"12345678-1234-1234-1234-123456789123","side":"1","orderType":"2","orderQuantity":10,"ticker":"APTK","classCode":"TQBR","price":9.530}""",
            handler.LastRequestContent);
        Assert.Equal(clientOrderId, response.ClientOrderId);
        Assert.Equal("OK", response.Status);
    }

    // Order status in the form of the method's response schema (https://trade-api.bcs.ru/http/operations/get-order-by-id):
    // reports as an array. The site has no ready response example for HTTP methods, only the schema; every field of a report
    // is filled. BCS itself sends one object (the test after this one).
    [Fact]
    public async Task GetOrderStatusAsync_ByClientOrderId_SendsGetWithQueryAndDeserializesReports()
    {
        const string statusJson = """
        {
          "clientOrderId": "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee",
          "originalClientOrderId": "12345678-1234-1234-9adb-123456789876",
          "data": [
            {
              "messageType": "8",
              "orderStatus": "0",
              "executionType": "0",
              "orderQuantity": 10,
              "executedQuantity": 0,
              "lastQuantity": 0,
              "remainedQuantity": 10,
              "ticker": "SBER",
              "classCode": "TQBR",
              "side": "1",
              "orderType": "2",
              "averagePrice": 0,
              "orderId": "20260501-TQBR-79628540663",
              "price": 300.5,
              "currency": "RUB",
              "clientCode": "123456",
              "transactionTime": "2026-05-01T07:00:00.000Z",
              "tradeDate": "2026-05-01",
              "orderNumber": "79628540663",
              "commission": 0,
              "securityExchange": "TQBR",
              "timeInForce": 1
            },
            {
              "messageType": "8",
              "orderStatus": "2",
              "executionType": "2",
              "orderQuantity": 10,
              "executedQuantity": 10,
              "lastQuantity": 10,
              "remainedQuantity": 0,
              "ticker": "SBER",
              "classCode": "TQBR",
              "side": "1",
              "orderType": "2",
              "averagePrice": 300.25,
              "orderId": "20260501-TQBR-79628540663",
              "executionId": "TQBR-Z3fE7c-S-1-1-N",
              "price": 300.5,
              "currency": "RUB",
              "clientCode": "123456",
              "transactionTime": "2026-05-01T07:00:01.250Z",
              "tradeDate": "2026-05-01",
              "orderNumber": "79628540663",
              "accruedCoupon": 1.5,
              "executionValue": 3002.5,
              "commission": 1.2,
              "securityExchange": "TQBR",
              "rejectReason": "",
              "timeInForce": 3,
              "expiryDate": "20260530",
              "stopPrice": 290.5,
              "takePrice": 310.5,
              "visible": 4,
              "spread": 0.5,
              "spreadType": 0,
              "offset": 1.5,
              "offsetType": 1
            }
          ]
        }
        """;

        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, statusJson)));
        var service = new BcsOrdersService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var response = await service.GetOrderStatusAsync(
            BcsOrderIdType.ClientOrderId,
            "12345678-1234-1234-9adb-123456789876",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal(
            new Uri("https://example.test/trade-api-bff-operations/api/v1/orders?orderIdType=1&orderId=12345678-1234-1234-9adb-123456789876"),
            handler.LastRequest?.RequestUri);
        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("access-token-1", handler.LastRequest?.Headers.Authorization?.Parameter);
        Assert.Null(handler.LastRequestContent);
        Assert.Equal("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee", response.ClientOrderId);
        Assert.Equal("12345678-1234-1234-9adb-123456789876", response.OriginalClientOrderId);
        Assert.Equal(2, response.Data.Count);

        var accepted = response.Data[0];
        Assert.Equal("0", accepted.OrderStatus);
        Assert.Equal("0", accepted.ExecutionType);
        Assert.Equal(0m, accepted.ExecutedQuantity);
        Assert.Null(accepted.ExecutionId);
        Assert.Equal(1, accepted.TimeInForce);
        Assert.Null(accepted.ExpiryDate);

        var filled = response.Data[1];
        Assert.Equal("8", filled.MessageType);
        Assert.Equal("2", filled.OrderStatus);
        Assert.Equal("2", filled.ExecutionType);
        Assert.Equal(10m, filled.OrderQuantity);
        Assert.Equal(10m, filled.ExecutedQuantity);
        Assert.Equal(10m, filled.LastQuantity);
        Assert.Equal(0m, filled.RemainedQuantity);
        Assert.Equal("SBER", filled.Ticker);
        Assert.Equal("TQBR", filled.ClassCode);
        Assert.Equal("1", filled.Side);
        Assert.Equal("2", filled.OrderType);
        Assert.Equal(300.25m, filled.AveragePrice);
        Assert.Equal("20260501-TQBR-79628540663", filled.OrderId);
        Assert.Equal("TQBR-Z3fE7c-S-1-1-N", filled.ExecutionId);
        Assert.Equal(300.5m, filled.Price);
        Assert.Equal("RUB", filled.Currency);
        Assert.Equal("123456", filled.ClientCode);
        Assert.Equal(new DateTimeOffset(2026, 05, 01, 07, 00, 01, 250, TimeSpan.Zero), filled.TransactionTime);
        Assert.Equal(new DateOnly(2026, 05, 01), filled.TradeDate);
        Assert.Equal("79628540663", filled.OrderNumber);
        Assert.Equal(1.5m, filled.AccruedCoupon);
        Assert.Equal(3002.5m, filled.ExecutionValue);
        Assert.Equal(1.2m, filled.Commission);
        Assert.Equal("TQBR", filled.SecurityExchange);
        Assert.Equal("", filled.RejectReason);
        Assert.Equal(3, filled.TimeInForce);
        Assert.Equal("20260530", filled.ExpiryDate);
        Assert.Equal(290.5m, filled.StopPrice);
        Assert.Equal(310.5m, filled.TakePrice);
        Assert.Equal(4, filled.Visible);
        Assert.Equal(0.5m, filled.Spread);
        Assert.Equal(0, filled.SpreadType);
        Assert.Equal(1.5m, filled.Offset);
        Assert.Equal(1, filled.OffsetType);
    }

    // BUG-63: BCS sends data as one object — the order after its latest event. On the account (2026-10-26) reading it as a
    // list failed with «could not be converted to IReadOnlyList…», Path: $.data, BytePositionInLine: 64 — the first byte of
    // the value after {"clientOrderId":"<UUID>","data":, as in this answer.
    [Fact]
    public async Task GetOrderStatusAsync_DataAsOneObject_IsTheOrdersOneReport()
    {
        const string statusJson =
            """{"clientOrderId":"12345678-1234-1234-9adb-123456789876","data":{"messageType":"8","orderStatus":"2","executionType":"2","orderQuantity":100,"executedQuantity":100,"lastQuantity":100,"remainedQuantity":0,"ticker":"SGZH","classCode":"TQBR","side":"1","orderType":"1","averagePrice":0.7455,"orderId":"20261026-TQBR-123","executionId":"TQBR-Z3fE7c-S-1-1-N","price":0,"currency":"RUB","clientCode":"123456","transactionTime":"2026-10-26T13:12:34.400Z","tradeDate":"2026-10-26","orderNumber":"123","executionValue":74.55,"commission":0.04,"securityExchange":"TQBR"},"originalClientOrderId":"12345678-1234-1234-9adb-123456789876"}""";
        Assert.Equal('{', statusJson[63]);

        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, statusJson)));
        var service = new BcsOrdersService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var response = await service.GetOrderStatusAsync(
            BcsOrderIdType.ClientOrderId,
            "12345678-1234-1234-9adb-123456789876",
            TestContext.Current.CancellationToken);

        Assert.Equal("12345678-1234-1234-9adb-123456789876", response.OriginalClientOrderId);
        var report = Assert.Single(response.Data);
        Assert.Equal("2", report.OrderStatus);
        Assert.Equal(100m, report.ExecutedQuantity);
        Assert.Equal(100m, report.LastQuantity);
        Assert.Equal(0.7455m, report.AveragePrice);
        Assert.Equal("20261026-TQBR-123", report.OrderId);
        Assert.Equal("TQBR-Z3fE7c-S-1-1-N", report.ExecutionId);
        Assert.Null(response.AdditionalFields);
        Assert.Null(report.AdditionalFields);
    }

    // data that is not a report or reports: the answer does not describe the order — an error that shows the answer.
    [Theory]
    [InlineData("""{"clientOrderId":"12345678-1234-1234-9adb-123456789876","data":"2"}""")]
    [InlineData("""{"clientOrderId":"12345678-1234-1234-9adb-123456789876","data":2}""")]
    [InlineData("""{"clientOrderId":"12345678-1234-1234-9adb-123456789876","data":null}""")]
    [InlineData("""{"clientOrderId":"12345678-1234-1234-9adb-123456789876","data":[null]}""")]
    [InlineData("""{"clientOrderId":"12345678-1234-1234-9adb-123456789876"}""")]
    [InlineData("null")]
    public async Task GetOrderStatusAsync_DataNotAReport_ThrowsWithTheAnswer(string statusJson)
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, statusJson)));
        var service = new BcsOrdersService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var exception = await Assert.ThrowsAsync<BcsResponseFormatException>(() => service.GetOrderStatusAsync(
            BcsOrderIdType.ClientOrderId,
            "12345678-1234-1234-9adb-123456789876",
            TestContext.Current.CancellationToken));

        Assert.Equal("order-status", exception.Endpoint);
        Assert.Contains("BCS order-status: the answer cannot be read", exception.Message);
        Assert.Contains(statusJson, exception.Message);
        Assert.DoesNotContain("access-token-1", exception.Message);
    }

    // Fields the contract does not name are kept: an answer of an unexpected form shows them.
    [Fact]
    public async Task GetOrderStatusAsync_UnknownFields_AreKept()
    {
        const string statusJson =
            """{"clientOrderId":"12345678-1234-1234-9adb-123456789876","requestState":"DONE","data":{"orderStatus":"0","order":{"status":"NEW"}}}""";
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, statusJson)));
        var service = new BcsOrdersService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var response = await service.GetOrderStatusAsync(
            BcsOrderIdType.ClientOrderId,
            "12345678-1234-1234-9adb-123456789876",
            TestContext.Current.CancellationToken);

        Assert.Equal("DONE", response.AdditionalFields?["requestState"].GetString());
        var report = Assert.Single(response.Data);
        Assert.Equal("0", report.OrderStatus);
        Assert.Equal("NEW", report.AdditionalFields?["order"].GetProperty("status").GetString());
    }

    // Every method: an answer the SDK cannot read is in the error, a long one cut with its length.
    [Fact]
    public async Task SearchOrdersAsync_AnswerThatCannotBeRead_ThrowsWithTheAnswerCut()
    {
        var answer = """{"records":{"orderId":"1"},"padding":""" + "\"" + new string('x', 3000) + "\"}";
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, answer)));
        var service = new BcsOrdersService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var exception = await Assert.ThrowsAsync<BcsResponseFormatException>(() => service.SearchOrdersAsync(
            new BcsOrdersSearchRequest(), page: 0, size: 100, sort: null, TestContext.Current.CancellationToken));

        Assert.Equal("orders-search", exception.Endpoint);
        Assert.Equal("$.records", exception.Path);
        Assert.Contains(answer[..BcsResponseFormatException.MaxShownLength], exception.Message);
        Assert.DoesNotContain(answer, exception.Message);
        Assert.Contains($"(the first {BcsResponseFormatException.MaxShownLength} of {answer.Length} characters)", exception.Message);
    }

    [Fact]
    public async Task GetOrderStatusAsync_ByExchangeOrderId_SendsTheIdentifierWithType2()
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, """{"clientOrderId":"aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee","data":[]}""")));
        var service = new BcsOrdersService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var response = await service.GetOrderStatusAsync(
            BcsOrderIdType.ExchangeOrderId,
            "20260501-TQBR-79628540663",
            TestContext.Current.CancellationToken);

        Assert.Equal(
            new Uri("https://example.test/trade-api-bff-operations/api/v1/orders?orderIdType=2&orderId=20260501-TQBR-79628540663"),
            handler.LastRequest?.RequestUri);
        Assert.Empty(response.Data);
    }

    [Fact]
    public async Task GetOrderStatusAsync_ClientOrderId_IsSentInCanonicalForm()
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, """{"clientOrderId":"aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee","data":[]}""")));
        var service = new BcsOrdersService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        await service.GetOrderStatusAsync(
            BcsOrderIdType.ClientOrderId,
            "{12345678-1234-1234-9ADB-123456789876}",
            TestContext.Current.CancellationToken);

        Assert.Equal(
            new Uri("https://example.test/trade-api-bff-operations/api/v1/orders?orderIdType=1&orderId=12345678-1234-1234-9adb-123456789876"),
            handler.LastRequest?.RequestUri);
    }

    [Fact]
    public async Task GetOrderStatusAsync_OrderNotFound_Throws404WithTheApiError()
    {
        const string notFoundJson = """
        {
          "timestamp": 1777618800000,
          "traceId": "trace-404",
          "type": "NOT_FOUND",
          "errors": [],
          "displayOptions": {}
        }
        """;

        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.NotFound, notFoundJson)));
        var service = new BcsOrdersService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var exception = await Assert.ThrowsAsync<BcsApiException>(() => service.GetOrderStatusAsync(
            BcsOrderIdType.ClientOrderId,
            "12345678-1234-1234-9adb-123456789876",
            TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal("order-status", exception.Endpoint);
        Assert.Equal(BcsApiErrorTypes.NotFound, exception.ApiError?.Type);
        Assert.Equal("trace-404", exception.ApiError?.TraceId);
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData("not-a-uuid")]
    [InlineData("20260501-TQBR-79628540663")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task GetOrderStatusAsync_ClientOrderIdNotAUuid_ThrowsBeforeAnyRequest(string orderId)
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, "{}")));
        var service = new BcsOrdersService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetOrderStatusAsync(BcsOrderIdType.ClientOrderId, orderId, TestContext.Current.CancellationToken));
        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [InlineData(BcsOrderIdType.ClientOrderId, null)]
    [InlineData(BcsOrderIdType.ClientOrderId, "")]
    [InlineData(BcsOrderIdType.ExchangeOrderId, " ")]
    public async Task GetOrderStatusAsync_WithNullOrWhitespaceOrderId_Throws(BcsOrderIdType orderIdType, string? orderId)
    {
        var service = CreateService();

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            service.GetOrderStatusAsync(orderIdType, orderId!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetOrderStatusAsync_WithUnsupportedOrderIdType_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetOrderStatusAsync((BcsOrderIdType)3, "20260501-TQBR-79628540663", TestContext.Current.CancellationToken));
    }

    // Synchronous creation answers in the order-status format
    // (https://trade-api.bcs.ru/http/operations/sync/create-order-sync), here by the method's response schema — an array;
    // BCS sends one object (the test after this one).
    [Fact]
    public async Task CreateOrderSyncAsync_PostsTheCreationBodyAndDeserializesTheOrderState()
    {
        const string stateJson = """
        {
          "clientOrderId": "12345678-1234-1234-1234-123456789123",
          "originalClientOrderId": "12345678-1234-1234-1234-123456789123",
          "data": [
            {
              "messageType": "8",
              "orderStatus": "8",
              "executionType": "8",
              "orderQuantity": 10,
              "executedQuantity": 0,
              "lastQuantity": 0,
              "remainedQuantity": 0,
              "ticker": "APTK",
              "classCode": "TQBR",
              "side": "1",
              "orderType": "2",
              "averagePrice": 0,
              "orderId": "20260501-TQBR-79628540664",
              "price": 9.53,
              "currency": "RUB",
              "transactionTime": "2026-05-01T07:00:00.000Z",
              "tradeDate": "2026-05-01",
              "rejectReason": "Trading is closed",
              "timeInForce": 1
            }
          ]
        }
        """;

        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, stateJson)));
        var service = new BcsOrdersService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var response = await service.CreateOrderSyncAsync(CreateOrderRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal(
            new Uri("https://example.test/trade-api-bff-operations/api/v1/orders/sync"),
            handler.LastRequest?.RequestUri);
        Assert.Equal("access-token-1", handler.LastRequest?.Headers.Authorization?.Parameter);
        Assert.Equal("application/json", handler.LastRequest?.Content?.Headers.ContentType?.MediaType);
        Assert.Equal(
            """{"clientOrderId":"12345678-1234-1234-1234-123456789123","side":"1","orderType":"2","orderQuantity":10,"ticker":"APTK","classCode":"TQBR","price":9.530}""",
            handler.LastRequestContent);
        Assert.Equal("12345678-1234-1234-1234-123456789123", response.ClientOrderId);
        var report = Assert.Single(response.Data);
        Assert.Equal("8", report.OrderStatus);
        Assert.Equal("Trading is closed", report.RejectReason);
        Assert.Equal("20260501-TQBR-79628540664", report.OrderId);
    }

    // BUG-63: the answer of the synchronous creation as BCS sends it — data is one object.
    [Fact]
    public async Task CreateOrderSyncAsync_DataAsOneObject_IsTheOrderState()
    {
        const string stateJson =
            """{"clientOrderId":"12345678-1234-1234-1234-123456789123","data":{"messageType":"8","orderStatus":"8","executionType":"8","orderQuantity":10,"executedQuantity":0,"lastQuantity":0,"remainedQuantity":0,"ticker":"APTK","classCode":"TQBR","side":"1","orderType":"2","averagePrice":0,"orderId":"20260501-TQBR-79628540664","price":9.53,"currency":"RUB","transactionTime":"2026-05-01T07:00:00.000Z","tradeDate":"2026-05-01","rejectReason":"Trading is closed"}}""";
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, stateJson)));
        var service = new BcsOrdersService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var response = await service.CreateOrderSyncAsync(CreateOrderRequest(), TestContext.Current.CancellationToken);

        var report = Assert.Single(response.Data);
        Assert.Equal("8", report.OrderStatus);
        Assert.Equal("Trading is closed", report.RejectReason);
        Assert.Equal("20260501-TQBR-79628540664", report.OrderId);
    }

    [Fact]
    public async Task CreateOrderSyncAsync_ValidatesTheRequestLikeCreation()
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, "{}")));
        var service = new BcsOrdersService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.CreateOrderSyncAsync(null!, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateOrderSyncAsync(CreateOrderRequest() with { ClientOrderId = Guid.Empty }, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.CreateOrderSyncAsync(CreateOrderRequest() with { OrderType = (BcsCreateOrderType)4 }, TestContext.Current.CancellationToken));
        Assert.Equal(0, handler.RequestCount);
    }

    // BUG-26: creation and the order list number order types differently; each request carries its own codes.
    [Theory]
    [InlineData(BcsCreateOrderType.Market, "1")]
    [InlineData(BcsCreateOrderType.Limit, "2")]
    public async Task CreateOrderAsync_SendsTheCreationCodeOfTheOrderType(BcsCreateOrderType orderType, string code)
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, """{"clientOrderId":"12345678-1234-1234-1234-123456789123","status":"OK"}""")));
        var service = new BcsOrdersService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        await service.CreateOrderAsync(CreateOrderRequest() with { OrderType = orderType }, TestContext.Current.CancellationToken);

        Assert.Contains($"\"orderType\":\"{code}\"", handler.LastRequestContent);
    }

    [Theory]
    [InlineData(BcsSearchOrderType.Market, 1)]
    [InlineData(BcsSearchOrderType.Limit, 2)]
    [InlineData(BcsSearchOrderType.Iceberg, 3)]
    [InlineData(BcsSearchOrderType.StopLimit, 4)]
    [InlineData(BcsSearchOrderType.TakeProfitWithLimitOrder, 5)]
    [InlineData(BcsSearchOrderType.StopLoss, 6)]
    [InlineData(BcsSearchOrderType.TakeProfitAndStopLoss, 7)]
    [InlineData(BcsSearchOrderType.LimitForThirtyDays, 10)]
    [InlineData(BcsSearchOrderType.TakeProfit, 11)]
    public async Task SearchOrdersAsync_SendsTheListCodeOfTheOrderType(BcsSearchOrderType orderType, int code)
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, """{"records":[],"totalRecords":0,"totalPages":0}""")));
        var service = new BcsOrdersService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        await service.SearchOrdersAsync(
            new BcsOrdersSearchRequest { OrderTypes = new[] { orderType } },
            page: 0,
            size: 50,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal($"{{\"orderTypes\":[{code}]}}", handler.LastRequestContent);
    }

    [Fact]
    public async Task SearchOrdersAsync_RecordOrderType_IsReadAsTheListType()
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                """{"records":[{"orderType":3},{"orderType":11}],"totalRecords":2,"totalPages":1}""")));
        var service = new BcsOrdersService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var orders = await service.SearchOrdersAsync(
            new BcsOrdersSearchRequest(),
            page: 0,
            size: 50,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            new BcsSearchOrderType?[] { BcsSearchOrderType.Iceberg, BcsSearchOrderType.TakeProfit },
            orders.Records.Select(order => order.OrderType));
    }

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
                OrderTypes = new[] { BcsSearchOrderType.Limit, BcsSearchOrderType.LimitForThirtyDays },
                Tickers = new[] { " SBER ", "GAZP" },
                ClassCodes = new[] { "TQBR" },
            },
            page: 2,
            size: 50,
            sort: new[] { BcsOrderSort.OrderDateTimeDesc, BcsOrderSort.TickerAsc },
            cancellationToken: TestContext.Current.CancellationToken);

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
        Assert.Equal(BcsSearchOrderType.Limit, order.OrderType);
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

        var orders = await service.SearchOrdersAsync(new BcsOrdersSearchRequest(), page: 0, size: 50, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            new Uri("https://example.test/trade-api-bff-order-details/api/v1/orders/search?page=0&size=50"),
            handler.LastRequest?.RequestUri);
        Assert.Equal("{}", handler.LastRequestContent);
        Assert.Empty(orders.Records);
    }

    [Fact]
    public async Task CreateOrderAsync_WithNullRequest_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.CreateOrderAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateOrderAsync_WithoutPrice_OmitsPriceFromBody()
    {
        var clientOrderId = Guid.Parse("12345678-1234-1234-1234-123456789123");
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, """{"clientOrderId":"12345678-1234-1234-1234-123456789123","status":"OK"}""")));
        var service = new BcsOrdersService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        await service.CreateOrderAsync(
            new BcsCreateOrderRequest
            {
                ClientOrderId = clientOrderId,
                Side = BcsOrderSide.Buy,
                OrderType = BcsCreateOrderType.Market,
                OrderQuantity = 10,
                Ticker = "APTK",
                ClassCode = "TQBR",
                // Price intentionally omitted for a market order.
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(
            """{"clientOrderId":"12345678-1234-1234-1234-123456789123","side":"1","orderType":"1","orderQuantity":10,"ticker":"APTK","classCode":"TQBR"}""",
            handler.LastRequestContent);
    }

    [Fact]
    public async Task CreateOrderAsync_WithEmptyClientOrderId_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateOrderAsync(CreateOrderRequest() with { ClientOrderId = Guid.Empty }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateOrderAsync_WithUnsupportedSide_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.CreateOrderAsync(CreateOrderRequest() with { Side = (BcsOrderSide)999 }, TestContext.Current.CancellationToken));
    }

    // BUG-26: codes 3-5 are other order types in creation than in the list; the SDK does not send them.
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(999)]
    public async Task CreateOrderAsync_WithUnsupportedOrderType_ThrowsBeforeAnyRequest(int orderType)
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, "{}")));
        var service = new BcsOrdersService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.CreateOrderAsync(CreateOrderRequest() with { OrderType = (BcsCreateOrderType)orderType }, TestContext.Current.CancellationToken));
        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task CreateOrderAsync_WithNullOrWhitespaceTicker_Throws(string? ticker)
    {
        var service = CreateService();

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            service.CreateOrderAsync(CreateOrderRequest() with { Ticker = ticker! }, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task CreateOrderAsync_WithNullOrWhitespaceClassCode_Throws(string? classCode)
    {
        var service = CreateService();

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            service.CreateOrderAsync(CreateOrderRequest() with { ClassCode = classCode! }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchOrdersAsync_WithNegativePage_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SearchOrdersAsync(new BcsOrdersSearchRequest(), page: -1, size: 50, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SearchOrdersAsync_WithInvalidSize_Throws(int size)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SearchOrdersAsync(new BcsOrdersSearchRequest(), page: 0, size: size, cancellationToken: TestContext.Current.CancellationToken));
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

        await service.SearchOrdersAsync(new BcsOrdersSearchRequest(), page: 0, size: 101, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            new Uri("https://example.test/trade-api-bff-order-details/api/v1/orders/search?page=0&size=101"),
            handler.LastRequest?.RequestUri);
    }

    [Fact]
    public async Task SearchOrdersAsync_WithNullRequest_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.SearchOrdersAsync(null!, page: 0, size: 50, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchOrdersAsync_WithUnsupportedSide_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SearchOrdersAsync(
                new BcsOrdersSearchRequest { Side = (BcsOrderSide)999 },
                page: 0,
                size: 50,
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchOrdersAsync_WithUnsupportedOrderStatus_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SearchOrdersAsync(
                new BcsOrdersSearchRequest { OrderStatus = new[] { (BcsOrderStatus)999 } },
                page: 0,
                size: 50,
                cancellationToken: TestContext.Current.CancellationToken));
    }

    // BUG-26: TrailingStop (12) is not a documented order type.
    [Theory]
    [InlineData(12)]
    [InlineData(999)]
    public async Task SearchOrdersAsync_WithUnsupportedOrderType_Throws(int orderType)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SearchOrdersAsync(
                new BcsOrdersSearchRequest { OrderTypes = new[] { (BcsSearchOrderType)orderType } },
                page: 0,
                size: 50,
                cancellationToken: TestContext.Current.CancellationToken));
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
                sort: new[] { (BcsOrderSort)999 },
                cancellationToken: TestContext.Current.CancellationToken));
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
                size: 50,
                cancellationToken: TestContext.Current.CancellationToken));
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
                size: 50,
                cancellationToken: TestContext.Current.CancellationToken));
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

    private static BcsCreateOrderRequest CreateOrderRequest() =>
        new()
        {
            ClientOrderId = Guid.Parse("12345678-1234-1234-1234-123456789123"),
            Side = BcsOrderSide.Buy,
            OrderType = BcsCreateOrderType.Limit,
            OrderQuantity = 10,
            Ticker = "APTK",
            ClassCode = "TQBR",
            Price = 9.530m,
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

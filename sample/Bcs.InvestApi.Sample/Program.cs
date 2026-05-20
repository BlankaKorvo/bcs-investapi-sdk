using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Bcs.InvestApi;
using Bcs.InvestApi.Contracts.Enums;
using Bcs.InvestApi.Contracts.Limits;
using Bcs.InvestApi.Contracts.MarginalIndicators;
using Bcs.InvestApi.Contracts.MarketData;
using Bcs.InvestApi.Contracts.Orders;
using Bcs.InvestApi.Contracts.Portfolio;
using Bcs.InvestApi.WebSockets;

var refreshToken = Environment.GetEnvironmentVariable("BCS_REFRESH_TOKEN");
if (string.IsNullOrWhiteSpace(refreshToken))
{
    Console.Error.WriteLine("Set BCS_REFRESH_TOKEN environment variable to your stable refresh/bootstrap secret.");
    return 1;
}

var classCode = Environment.GetEnvironmentVariable("BCS_SAMPLE_CLASS_CODE");
if (string.IsNullOrWhiteSpace(classCode))
{
    classCode = "TQBR";
}

var ticker = Environment.GetEnvironmentVariable("BCS_SAMPLE_TICKER");
if (string.IsNullOrWhiteSpace(ticker))
{
    ticker = "SBER";
}

Guid? orderStatusClientOrderId = null;
BcsCreateOrderRequest? createOrderRequest = null;
(Guid OriginalId, BcsUpdateOrderRequest Request)? updateOrderConfig = null;
(Guid OriginalId, Guid ClientOrderId)? cancelOrderConfig = null;
BcsAuthClientIds clientId;

try
{
    orderStatusClientOrderId = ParseOptionalGuid("BCS_SAMPLE_ORDER_STATUS_CLIENT_ORDER_ID");
    createOrderRequest = BuildCreateOrderRequest(ticker, classCode);
    updateOrderConfig = BuildUpdateOrderConfig();
    cancelOrderConfig = BuildCancelOrderConfig();

    var writeOperationEnabled = createOrderRequest is not null
        || updateOrderConfig is not null
        || cancelOrderConfig is not null;
    clientId = GetConfiguredClientId(writeOperationEnabled);
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

await using var client = BcsInvestApiClientFactory.Create(
    refreshToken: refreshToken,
    clientId: clientId);

var limits = await client.Limits.GetLimitsAsync();
Console.WriteLine($"Depo limits: {limits.DepoLimit.Count}");
Console.WriteLine($"Future holdings: {limits.FutureHolding.Count}");
Console.WriteLine($"Money limits: {limits.MoneyLimits.Count}");
Console.WriteLine($"Futures limits: {limits.FuturesLimits.Count}");

var portfolio = await client.Portfolio.GetPortfolioAsync();
Console.WriteLine($"Portfolio positions: {portfolio.Count}");

var isins = GetConfiguredIsins();
var instruments = await client.Instruments.GetInstrumentsByIsinsAsync(isins, page: 0, size: 50);
Console.WriteLine($"Instruments by ISIN page 0: {instruments.Count}");

foreach (var instrument in instruments.Take(10))
{
    Console.WriteLine(
        $"  {instrument.Isin}: {instrument.Ticker} {instrument.PrimaryBoard} {instrument.DisplayName}");
}

var tickers = GetConfiguredTickers();
var instrumentsByTicker = await client.Instruments.GetInstrumentsByTickersAsync(tickers, page: 0, size: 50);
Console.WriteLine($"Instruments by ticker page 0: {instrumentsByTicker.Count}");

foreach (var instrument in instrumentsByTicker.Take(10))
{
    Console.WriteLine(
        $"  {instrument.Ticker}: {instrument.Isin} {instrument.PrimaryBoard} {instrument.DisplayName}");
}

var stocksPage = await client.Instruments.GetInstrumentsByTypeAsync(
    BcsInstrumentTypes.Stock,
    page: 0,
    size: 10);
Console.WriteLine($"Instruments by type STOCK page: {stocksPage.Count}");

foreach (var instrument in stocksPage)
{
    Console.WriteLine(
        $"  {instrument.Ticker}: {instrument.Isin} {instrument.PrimaryBoard} {instrument.DisplayName}");
}

var candlesEnd = DateTimeOffset.UtcNow;
var candlesStart = candlesEnd.AddDays(-1);
var candles = await client.MarketData.GetCandlesAsync(
    classCode,
    ticker,
    candlesStart,
    candlesEnd,
    BcsCandleTimeFrames.Minute1);
Console.WriteLine($"Minute1 candles for {classCode}/{ticker}: {candles.Bars.Count}");

foreach (var bar in candles.Bars.Take(10))
{
    Console.WriteLine(
        $"  {bar.Time:O}: O={bar.Open} H={bar.High} L={bar.Low} C={bar.Close} V={bar.Volume}");
}

var schedule = await client.TradingSchedule.GetDailyTradingScheduleAsync(classCode, ticker);
Console.WriteLine($"Daily schedule for {classCode}/{ticker}: work day = {schedule.IsWorkDay}, intervals = {schedule.DailySchedule.Count}");

foreach (var interval in schedule.DailySchedule)
{
    Console.WriteLine(
        $"  {interval.StartDate:HH:mm:ss}-{interval.EndDate:HH:mm:ss}: {interval.TradingSessionType} ({interval.TradingSessionStatus})");
}

var ordersEnd = DateTimeOffset.UtcNow;
var ordersStart = ordersEnd.AddDays(-500);
var orders = await client.Orders.SearchOrdersAsync(
    new BcsOrdersSearchRequest
    {
        StartDateTime = ordersStart,
        EndDateTime = ordersEnd,
        //Tickers = new[] { ticker },
        //ClassCodes = new[] { classCode },
    },
    page: 0,
    size: 10,
    sort: new[] { BcsOrderSort.OrderDateTimeDesc });
Console.WriteLine($"Orders for {classCode}/{ticker}: page records = {orders.Records.Count}, total = {orders.TotalRecords}, pages = {orders.TotalPages}");

foreach (var order in orders.Records.Take(10))
{
    Console.WriteLine(
        $"  {order.OrderDateTime:O}: #{order.OrderNum} {order.Side} {order.OrderType} {order.OrderStatus} {order.Ticker}/{order.ClassCode} qty={order.OrderQuantity} price={order.Price}");
}

if (orderStatusClientOrderId is { } statusId)
{
    var statusResponse = await client.Orders.GetOrderStatusAsync(statusId);
    Console.WriteLine(
        $"Order status result: clientOrderId = {statusResponse.ClientOrderId}, originalClientOrderId = {statusResponse.OriginalClientOrderId}");

    if (statusResponse.Data is { } statusData)
    {
        Console.WriteLine(
            $"  {statusData.TransactionTime:O}: {statusData.Ticker}/{statusData.ClassCode} status={statusData.OrderStatus} execType={statusData.ExecutionType} qty={statusData.OrderQuantity} remained={statusData.RemainedQuantity} price={statusData.Price}");
    }
}
else
{
    Console.WriteLine("Order status skipped. Set BCS_SAMPLE_ORDER_STATUS_CLIENT_ORDER_ID to enable it.");
}

if (createOrderRequest is not null)
{
    var createResponse = await client.Orders.CreateOrderAsync(createOrderRequest);
    Console.WriteLine(
        $"Create order result: clientOrderId = {createResponse.ClientOrderId}, status = {createResponse.Status}");
}
else
{
    Console.WriteLine("Order create skipped. Set BCS_SAMPLE_CREATE_ORDER=true, BCS_SAMPLE_CREATE_ORDER_QUANTITY, and BCS_SAMPLE_CREATE_PRICE to enable it.");
}

if (updateOrderConfig is { } updateCfg)
{
    var updateResponse = await client.Orders.UpdateOrderAsync(updateCfg.OriginalId, updateCfg.Request);
    Console.WriteLine(
        $"Update order result: clientOrderId = {updateResponse.ClientOrderId}, status = {updateResponse.Status}");
}
else
{
    Console.WriteLine("Order update skipped. Set BCS_SAMPLE_UPDATE_ORIGINAL_CLIENT_ORDER_ID, BCS_SAMPLE_UPDATE_ORDER_QUANTITY, and BCS_SAMPLE_UPDATE_PRICE to enable it.");
}

if (cancelOrderConfig is { } cancelCfg)
{
    var cancelResponse = await client.Orders.CancelOrderAsync(cancelCfg.OriginalId, cancelCfg.ClientOrderId);
    Console.WriteLine($"Cancel order result: clientOrderId = {cancelResponse.ClientOrderId}, status = {cancelResponse.Status}");
}
else
{
    Console.WriteLine("Order cancel skipped. Set BCS_SAMPLE_CANCEL_ORIGINAL_CLIENT_ORDER_ID to enable it.");
}

if (IsEnabled("BCS_SAMPLE_STREAM_CANDLES"))
{
    await StreamLastCandlesAsync(client, classCode, ticker);
}
else
{
    Console.WriteLine("Last-candle stream skipped. Set BCS_SAMPLE_STREAM_CANDLES=true to enable it.");
}

if (IsEnabled("BCS_SAMPLE_STREAM_ORDERBOOK"))
{
    await StreamOrderBookAsync(client, classCode, ticker);
}
else
{
    Console.WriteLine("Order-book stream skipped. Set BCS_SAMPLE_STREAM_ORDERBOOK=true to enable it.");
}

if (IsEnabled("BCS_SAMPLE_STREAM_QUOTES"))
{
    await StreamQuotesAsync(client, classCode, ticker);
}
else
{
    Console.WriteLine("Quotes stream skipped. Set BCS_SAMPLE_STREAM_QUOTES=true to enable it.");
}

if (IsEnabled("BCS_SAMPLE_STREAM_TRADES"))
{
    await StreamLastTradesAsync(client, classCode, ticker);
}
else
{
    Console.WriteLine("Anonymous-trades stream skipped. Set BCS_SAMPLE_STREAM_TRADES=true to enable it.");
}

if (IsEnabled("BCS_SAMPLE_STREAM_LIMITS"))
{
    await StreamLimitsAsync(client);
}
else
{
    Console.WriteLine("Limits stream skipped. Set BCS_SAMPLE_STREAM_LIMITS=true to enable it.");
}

if (IsEnabled("BCS_SAMPLE_STREAM_PORTFOLIO"))
{
    await StreamPortfolioAsync(client);
}
else
{
    Console.WriteLine("Portfolio stream skipped. Set BCS_SAMPLE_STREAM_PORTFOLIO=true to enable it.");
}

if (IsEnabled("BCS_SAMPLE_STREAM_ORDER_EXECUTIONS"))
{
    await StreamOrderExecutionsAsync(client);
}
else
{
    Console.WriteLine("Order-execution stream skipped. Set BCS_SAMPLE_STREAM_ORDER_EXECUTIONS=true to enable it.");
}

if (IsEnabled("BCS_SAMPLE_STREAM_ORDER_TRANSACTIONS"))
{
    await StreamOrderTransactionsAsync(client);
}
else
{
    Console.WriteLine("Order-transaction stream skipped. Set BCS_SAMPLE_STREAM_ORDER_TRANSACTIONS=true to enable it.");
}

if (IsEnabled("BCS_SAMPLE_STREAM_MARGIN"))
{
    await StreamMarginalIndicatorsAsync(client);
}
else
{
    Console.WriteLine("Marginal-indicators stream skipped. Set BCS_SAMPLE_STREAM_MARGIN=true to enable it.");
}

return 0;

static async Task StreamLastCandlesAsync(BcsInvestApiClient client, string classCode, string ticker)
{
    Console.WriteLine($"Connecting to last-candle stream for {classCode}/{ticker} (M1)...");

    // The SDK provides connect + receive plumbing only; the caller owns the socket lifecycle.
    using var socket = await client.WebSockets.ConnectAsync(BcsWebSocketEndpoints.MarketData);

    var subscribe = new BcsMarketDataSubscribeRequest
    {
        SubscribeType = BcsMarketDataSubscribeType.Subscribe,
        DataType = BcsMarketDataType.Candles,
        TimeFrame = "M1",
        Instruments = new[]
        {
            new BcsMarketDataInstrument { ClassCode = classCode, Ticker = ticker },
        },
    };

    var payload = JsonSerializer.SerializeToUtf8Bytes(subscribe, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    await socket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
    Console.WriteLine("Subscribed; waiting for up to 5 candle messages (30s timeout)...");

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    try
    {
        for (var received = 0; received < 5; received++)
        {
            var message = await socket.ReceiveJsonMessageAsync<BcsLastCandleMessage>(cts.Token);
            if (message is null)
            {
                Console.WriteLine("  Stream closed by server.");
                break;
            }

            if (message.Errors.Count > 0)
            {
                foreach (var error in message.Errors)
                {
                    Console.WriteLine($"  Error [{error.Code}]: {error.Message}");
                }

                break;
            }

            if (string.Equals(message.ResponseType, "CandleStickSuccess", StringComparison.Ordinal))
            {
                Console.WriteLine(
                    $"  Subscription confirmed for {message.Ticker}/{message.ClassCode} {message.TimeFrame} at {message.DateTime:O}");
                received--; // Acks don't count toward the candle quota.
                continue;
            }

            Console.WriteLine(
                $"  {message.DateTime:O} {message.Ticker}/{message.ClassCode}: O={message.Open} H={message.High} L={message.Low} C={message.Close} V={message.Volume}");
        }

        // Politely unsubscribe with the same payload shape.
        var unsubscribe = subscribe with { SubscribeType = BcsMarketDataSubscribeType.Unsubscribe };
        var unsubscribePayload = JsonSerializer.SerializeToUtf8Bytes(unsubscribe, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await socket.SendAsync(unsubscribePayload, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("  Timed out waiting for candle messages.");
    }
}

static async Task StreamOrderBookAsync(BcsInvestApiClient client, string classCode, string ticker)
{
    Console.WriteLine($"Connecting to order-book stream for {classCode}/{ticker} (depth 20)...");

    using var socket = await client.WebSockets.ConnectAsync(BcsWebSocketEndpoints.MarketData);

    var subscribe = new BcsOrderBookSubscribeRequest
    {
        SubscribeType = BcsMarketDataSubscribeType.Subscribe,
        DataType = BcsMarketDataType.OrderBook,
        Depth = 20,
        Instruments = new[]
        {
            new BcsMarketDataInstrument { ClassCode = classCode, Ticker = ticker },
        },
    };

    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    await socket.SendAsync(
        JsonSerializer.SerializeToUtf8Bytes(subscribe, options),
        WebSocketMessageType.Text,
        endOfMessage: true,
        CancellationToken.None);
    Console.WriteLine("Subscribed; waiting for up to 5 order-book messages (30s timeout)...");

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    try
    {
        for (var received = 0; received < 5; received++)
        {
            var message = await socket.ReceiveJsonMessageAsync<BcsOrderBookMessage>(cts.Token);
            if (message is null)
            {
                Console.WriteLine("  Stream closed by server.");
                break;
            }

            if (message.Errors.Count > 0)
            {
                foreach (var error in message.Errors)
                {
                    Console.WriteLine($"  Error [{error.Code}]: {error.Message}");
                }

                break;
            }

            if (string.Equals(message.ResponseType, "OrderBookSuccess", StringComparison.Ordinal))
            {
                Console.WriteLine(
                    $"  Subscription confirmed for {message.Ticker}/{message.ClassCode} depth={message.Depth} at {message.DateTime:O}");
                received--; // Acks don't count toward the message quota.
                continue;
            }

            var bestBid = message.Bids.Count > 0 ? message.Bids[0] : null;
            var bestAsk = message.Asks.Count > 0 ? message.Asks[0] : null;
            Console.WriteLine(
                $"  {message.DateTime:O} {message.Ticker}/{message.ClassCode}: " +
                $"bestBid={bestBid?.Price}x{bestBid?.Quantity} bestAsk={bestAsk?.Price}x{bestAsk?.Quantity} " +
                $"bidVol={message.BidVolume} askVol={message.AskVolume} levels={message.Bids.Count}/{message.Asks.Count}");
        }

        var unsubscribe = subscribe with { SubscribeType = BcsMarketDataSubscribeType.Unsubscribe };
        await socket.SendAsync(
            JsonSerializer.SerializeToUtf8Bytes(unsubscribe, options),
            WebSocketMessageType.Text,
            endOfMessage: true,
            CancellationToken.None);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("  Timed out waiting for order-book messages.");
    }
}

static async Task StreamQuotesAsync(BcsInvestApiClient client, string classCode, string ticker)
{
    Console.WriteLine($"Connecting to quotes stream for {classCode}/{ticker}...");

    using var socket = await client.WebSockets.ConnectAsync(BcsWebSocketEndpoints.MarketData);

    var subscribe = new BcsQuotesSubscribeRequest
    {
        SubscribeType = BcsMarketDataSubscribeType.Subscribe,
        DataType = BcsMarketDataType.Quotes,
        Instruments = new[]
        {
            new BcsMarketDataInstrument { ClassCode = classCode, Ticker = ticker },
        },
    };

    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    await socket.SendAsync(
        JsonSerializer.SerializeToUtf8Bytes(subscribe, options),
        WebSocketMessageType.Text,
        endOfMessage: true,
        CancellationToken.None);
    Console.WriteLine("Subscribed; waiting for up to 5 quote messages (30s timeout)...");

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    try
    {
        for (var received = 0; received < 5; received++)
        {
            var message = await socket.ReceiveJsonMessageAsync<BcsQuotesMessage>(cts.Token);
            if (message is null)
            {
                Console.WriteLine("  Stream closed by server.");
                break;
            }

            if (message.Errors.Count > 0)
            {
                foreach (var error in message.Errors)
                {
                    Console.WriteLine($"  Error [{error.Code}]: {error.Message}");
                }

                break;
            }

            Console.WriteLine(
                $"  {message.DateTime:O} {message.Ticker}/{message.ClassCode}: " +
                $"last={message.Last} bid={message.Bid} offer={message.Offer} " +
                $"open={message.Open} high={message.High} low={message.Low} change={message.Change} ({message.ChangeRate}%) status={message.SecurityTradingStatus}");
        }

        var unsubscribe = subscribe with { SubscribeType = BcsMarketDataSubscribeType.Unsubscribe };
        await socket.SendAsync(
            JsonSerializer.SerializeToUtf8Bytes(unsubscribe, options),
            WebSocketMessageType.Text,
            endOfMessage: true,
            CancellationToken.None);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("  Timed out waiting for quote messages.");
    }
}

static async Task StreamLastTradesAsync(BcsInvestApiClient client, string classCode, string ticker)
{
    Console.WriteLine($"Connecting to anonymous-trades stream for {classCode}/{ticker}...");

    using var socket = await client.WebSockets.ConnectAsync(BcsWebSocketEndpoints.MarketData);

    var subscribe = new BcsLastTradesSubscribeRequest
    {
        SubscribeType = BcsMarketDataSubscribeType.Subscribe,
        DataType = BcsMarketDataType.AnonymousTrades,
        Instruments = new[]
        {
            new BcsMarketDataInstrument { ClassCode = classCode, Ticker = ticker },
        },
    };

    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    await socket.SendAsync(
        JsonSerializer.SerializeToUtf8Bytes(subscribe, options),
        WebSocketMessageType.Text,
        endOfMessage: true,
        CancellationToken.None);
    Console.WriteLine("Subscribed; waiting for up to 5 trade messages (30s timeout)...");

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    try
    {
        for (var received = 0; received < 5; received++)
        {
            var message = await socket.ReceiveJsonMessageAsync<BcsLastTradeMessage>(cts.Token);
            if (message is null)
            {
                Console.WriteLine("  Stream closed by server.");
                break;
            }

            if (message.Errors.Count > 0)
            {
                foreach (var error in message.Errors)
                {
                    Console.WriteLine($"  Error [{error.Code}]: {error.Message}");
                }

                break;
            }

            if (string.Equals(message.ResponseType, "LastTradesSuccess", StringComparison.Ordinal))
            {
                Console.WriteLine(
                    $"  Subscription confirmed for {message.Ticker}/{message.ClassCode} at {message.DateTime:O}");
                received--; // Acks don't count toward the message quota.
                continue;
            }

            Console.WriteLine(
                $"  {message.DateTime:O} {message.Ticker}/{message.ClassCode}: " +
                $"{message.Side} price={message.Price} qty={message.Quantity} volume={message.Volume}");
        }

        var unsubscribe = subscribe with { SubscribeType = BcsMarketDataSubscribeType.Unsubscribe };
        await socket.SendAsync(
            JsonSerializer.SerializeToUtf8Bytes(unsubscribe, options),
            WebSocketMessageType.Text,
            endOfMessage: true,
            CancellationToken.None);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("  Timed out waiting for trade messages.");
    }
}

static async Task StreamLimitsAsync(BcsInvestApiClient client)
{
    Console.WriteLine("Connecting to limits stream...");

    // Limits/portfolio streams push the same payload as their HTTP endpoints; no subscribe message is required.
    using var socket = await client.WebSockets.ConnectAsync(BcsWebSocketEndpoints.Limits);
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

    try
    {
        for (var received = 0; received < 3; received++)
        {
            var limits = await socket.ReceiveJsonMessageAsync<BcsLimitsResponse>(cts.Token);
            if (limits is null)
            {
                Console.WriteLine("  Stream closed by server.");
                break;
            }

            Console.WriteLine(
                $"  Update: depo={limits.DepoLimit.Count}, futureHoldings={limits.FutureHolding.Count}, money={limits.MoneyLimits.Count}, futures={limits.FuturesLimits.Count}");
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("  Timed out waiting for limits updates.");
    }
}

static async Task StreamPortfolioAsync(BcsInvestApiClient client)
{
    Console.WriteLine("Connecting to portfolio stream...");

    using var socket = await client.WebSockets.ConnectAsync(BcsWebSocketEndpoints.Portfolio);
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

    try
    {
        for (var received = 0; received < 3; received++)
        {
            // Each portfolio message is an array of positions, matching the HTTP /portfolio payload.
            var positions = await socket.ReceiveJsonMessageAsync<BcsPortfolioItem[]>(cts.Token);
            if (positions is null)
            {
                Console.WriteLine("  Stream closed by server.");
                break;
            }

            Console.WriteLine($"  Update: {positions.Length} positions");
            foreach (var position in positions.Take(10))
            {
                Console.WriteLine(
                    $"    {position.Ticker}/{position.Board}: qty={position.Quantity} current={position.CurrentValue} pl={position.UnrealizedPL}");
            }
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("  Timed out waiting for portfolio updates.");
    }
}

static async Task StreamMarginalIndicatorsAsync(BcsInvestApiClient client)
{
    Console.WriteLine("Connecting to marginal-indicators stream...");

    // Pushes a full margin snapshot after connect; no subscribe message is required.
    using var socket = await client.WebSockets.ConnectAsync(BcsWebSocketEndpoints.MarginalIndicators);
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

    try
    {
        for (var received = 0; received < 3; received++)
        {
            var snapshot = await socket.ReceiveJsonMessageAsync<BcsMarginalIndicators>(cts.Token);
            if (snapshot is null)
            {
                Console.WriteLine("  Stream closed by server.");
                break;
            }

            var value = snapshot.PortfolioCurrentValue;
            var stability = snapshot.PortfolioStability;
            Console.WriteLine(
                $"  Account {snapshot.AgreementData?.Number} ({snapshot.AgreementData?.DisplayName}): " +
                $"value={value?.CurrentValueRub}₽ daily={value?.DailyPortfolioChangePerc}% " +
                $"marginOn={stability?.IsMarginOn} status={stability?.StabilityStatusName} " +
                $"securities={snapshot.MarginSecurities?.MarginBySecurity.Count ?? 0} futures={snapshot.FuturesParameters?.FuturesPortfolio.Count ?? 0}");
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("  Timed out waiting for marginal-indicators updates.");
    }
}

static Task StreamOrderExecutionsAsync(BcsInvestApiClient client) =>
    StreamOrderStatusAsync(client, BcsWebSocketEndpoints.OrderExecutions, "order-execution");

static Task StreamOrderTransactionsAsync(BcsInvestApiClient client) =>
    StreamOrderStatusAsync(client, BcsWebSocketEndpoints.OrderTransactions, "order-transaction");

static async Task StreamOrderStatusAsync(BcsInvestApiClient client, Uri url, string label)
{
    Console.WriteLine($"Connecting to {label} stream...");

    // Pushes the same payload as the HTTP order-status endpoint; no subscribe message is required.
    using var socket = await client.WebSockets.ConnectAsync(url);
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

    try
    {
        for (var received = 0; received < 5; received++)
        {
            var update = await socket.ReceiveJsonMessageAsync<BcsOrderStatusResponse>(cts.Token);
            if (update is null)
            {
                Console.WriteLine("  Stream closed by server.");
                break;
            }

            var data = update.Data;
            Console.WriteLine(
                $"  clientOrderId={update.ClientOrderId} original={update.OriginalClientOrderId}: " +
                (data is null
                    ? "(no data)"
                    : $"{data.TransactionTime:O} {data.Ticker}/{data.ClassCode} status={data.OrderStatus} execType={data.ExecutionType} " +
                      $"qty={data.OrderQuantity} executed={data.ExecutedQuantity} remained={data.RemainedQuantity} price={data.Price}"));
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine($"  Timed out waiting for {label} updates.");
    }
}

static BcsAuthClientIds GetConfiguredClientId(bool writeOperationEnabled)
{
    var rawClientId = Environment.GetEnvironmentVariable("BCS_SAMPLE_CLIENT_ID");
    if (string.IsNullOrWhiteSpace(rawClientId))
    {
        return writeOperationEnabled
            ? BcsAuthClientIds.TradeApiWrite
            : BcsAuthClientIds.TradeApiRead;
    }

    return rawClientId.Trim().ToLowerInvariant() switch
    {
        "read" => BcsAuthClientIds.TradeApiRead,
        "write" => BcsAuthClientIds.TradeApiWrite,
        "trade-api-read" => BcsAuthClientIds.TradeApiRead,
        "trade-api-write" => BcsAuthClientIds.TradeApiWrite,
        _ => throw new InvalidOperationException("BCS_SAMPLE_CLIENT_ID must be read, write, trade-api-read, or trade-api-write."),
    };
}

static BcsCreateOrderRequest? BuildCreateOrderRequest(string ticker, string classCode)
{
    if (!IsEnabled("BCS_SAMPLE_CREATE_ORDER"))
    {
        return null;
    }

    return new BcsCreateOrderRequest
    {
        ClientOrderId = ParseGuidWithDefault("BCS_SAMPLE_CREATE_CLIENT_ORDER_ID", Guid.NewGuid),
        Side = ParseEnumOrDefault("BCS_SAMPLE_CREATE_SIDE", BcsOrderSide.Buy),
        OrderType = ParseEnumOrDefault("BCS_SAMPLE_CREATE_ORDER_TYPE", BcsOrderType.Limit),
        OrderQuantity = ParseRequiredPositiveInt64("BCS_SAMPLE_CREATE_ORDER_QUANTITY"),
        Price = ParseRequiredPositiveDecimal("BCS_SAMPLE_CREATE_PRICE"),
        Ticker = ticker,
        ClassCode = classCode,
    };
}

static (Guid OriginalId, BcsUpdateOrderRequest Request)? BuildUpdateOrderConfig()
{
    var originalId = ParseOptionalGuid("BCS_SAMPLE_UPDATE_ORIGINAL_CLIENT_ORDER_ID");
    if (originalId is null)
    {
        return null;
    }

    var request = new BcsUpdateOrderRequest
    {
        ClientOrderId = ParseGuidWithDefault("BCS_SAMPLE_UPDATE_CLIENT_ORDER_ID", Guid.NewGuid),
        OrderQuantity = ParseRequiredPositiveInt64("BCS_SAMPLE_UPDATE_ORDER_QUANTITY"),
        Price = ParseRequiredPositiveDecimal("BCS_SAMPLE_UPDATE_PRICE"),
    };

    return (originalId.Value, request);
}

static (Guid OriginalId, Guid ClientOrderId)? BuildCancelOrderConfig()
{
    var originalId = ParseOptionalGuid("BCS_SAMPLE_CANCEL_ORIGINAL_CLIENT_ORDER_ID");
    if (originalId is null)
    {
        return null;
    }

    var clientOrderId = ParseGuidWithDefault("BCS_SAMPLE_CANCEL_CLIENT_ORDER_ID", Guid.NewGuid);
    return (originalId.Value, clientOrderId);
}

static bool IsEnabled(string variableName)
{
    var rawValue = Environment.GetEnvironmentVariable(variableName);
    if (string.IsNullOrWhiteSpace(rawValue))
    {
        return false;
    }

    return rawValue.Trim().ToLowerInvariant() switch
    {
        "1" or "true" or "yes" or "on" => true,
        "0" or "false" or "no" or "off" => false,
        _ => throw new InvalidOperationException(
            $"{variableName} must be one of: 1/0, true/false, yes/no, on/off."),
    };
}

static Guid? ParseOptionalGuid(string variableName)
{
    var rawValue = Environment.GetEnvironmentVariable(variableName);
    if (string.IsNullOrWhiteSpace(rawValue))
    {
        return null;
    }

    if (!Guid.TryParse(rawValue, out var value))
    {
        throw new InvalidOperationException($"{variableName} must be a valid UUID.");
    }

    return value;
}

static Guid ParseGuidWithDefault(string variableName, Func<Guid> defaultFactory)
{
    var rawValue = Environment.GetEnvironmentVariable(variableName);
    if (string.IsNullOrWhiteSpace(rawValue))
    {
        return defaultFactory();
    }

    if (!Guid.TryParse(rawValue, out var value))
    {
        throw new InvalidOperationException($"{variableName} must be a valid UUID.");
    }

    return value;
}

static long ParseRequiredPositiveInt64(string variableName)
{
    var rawValue = Environment.GetEnvironmentVariable(variableName);
    if (!long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value <= 0)
    {
        throw new InvalidOperationException($"{variableName} must be a positive integer.");
    }

    return value;
}

static decimal ParseRequiredPositiveDecimal(string variableName)
{
    var rawValue = Environment.GetEnvironmentVariable(variableName);
    if (!decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || value <= 0)
    {
        throw new InvalidOperationException($"{variableName} must be a positive decimal number with '.' as decimal separator.");
    }

    return value;
}

static TEnum ParseEnumOrDefault<TEnum>(string variableName, TEnum defaultValue)
    where TEnum : struct, Enum
{
    var rawValue = Environment.GetEnvironmentVariable(variableName);
    if (string.IsNullOrWhiteSpace(rawValue))
    {
        return defaultValue;
    }

    if (Enum.TryParse<TEnum>(rawValue, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
    {
        return parsed;
    }

    var names = string.Join(", ", Enum.GetNames<TEnum>());
    throw new InvalidOperationException(
        $"{variableName} must be one of: {names} (case-insensitive), or a numeric value of the underlying enum.");
}

static string[] GetConfiguredIsins()
{
    var rawIsins = Environment.GetEnvironmentVariable("BCS_SAMPLE_ISINS");
    if (string.IsNullOrWhiteSpace(rawIsins))
    {
        return new[] { "RU0009029540", "RU0007661625", "RU000A0J2Q06" };
    }

    return rawIsins
        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

static string[] GetConfiguredTickers()
{
    var rawTickers = Environment.GetEnvironmentVariable("BCS_SAMPLE_TICKERS");
    if (string.IsNullOrWhiteSpace(rawTickers))
    {
        return new[] { "SBER", "GAZP", "ROSN" };
    }

    return rawTickers
        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

using System.Globalization;
using Bcs.InvestApi;
using Bcs.InvestApi.Contracts.Enums;
using Bcs.InvestApi.Contracts.Limits;
using Bcs.InvestApi.Contracts.MarginalIndicators;
using Bcs.InvestApi.Contracts.MarketData;
using Bcs.InvestApi.Contracts.Orders;
using Bcs.InvestApi.Contracts.Portfolio;
using Bcs.InvestApi.Contracts.Trades;
using Bcs.InvestApi.WebSockets;

// ---------------------------------------------------------------------------
// Bootstrap: read configuration from environment variables.
// ---------------------------------------------------------------------------
var refreshToken = Environment.GetEnvironmentVariable("BCS_REFRESH_TOKEN");
if (string.IsNullOrWhiteSpace(refreshToken))
{
    Console.Error.WriteLine("Set BCS_REFRESH_TOKEN environment variable to your stable refresh/bootstrap secret.");
    return 1;
}

var classCode = GetEnvOrDefault("BCS_SAMPLE_CLASS_CODE", "TQBR");
var ticker = GetEnvOrDefault("BCS_SAMPLE_TICKER", "SBER");

Guid? orderStatusClientOrderId;
BcsCreateOrderRequest? createOrderRequest;
(Guid OriginalId, BcsUpdateOrderRequest Request)? updateOrderConfig;
(Guid OriginalId, Guid ClientOrderId)? cancelOrderConfig;
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

await using var client = BcsInvestApiClientFactory.Create(refreshToken: refreshToken, clientId: clientId);

// ---------------------------------------------------------------------------
// HTTP endpoints. Each call maps to one BCS Trade API request.
// ---------------------------------------------------------------------------
await ShowLimitsAsync(client);
await ShowPortfolioAsync(client);
await ShowInstrumentsAsync(client);
await ShowCandlesAsync(client, classCode, ticker);
await ShowTradingScheduleAsync(client, classCode, ticker);
await ShowOrdersAsync(client, classCode, ticker);
await ShowTradesAsync(client);
await ShowMarginDiscountsAsync(client);
await ShowOrderStatusAsync(client, orderStatusClientOrderId);
await ShowCreateOrderAsync(client, createOrderRequest);
await ShowUpdateOrderAsync(client, updateOrderConfig);
await ShowCancelOrderAsync(client, cancelOrderConfig);

// ---------------------------------------------------------------------------
// WebSocket streams. Each is opt-in via a BCS_SAMPLE_STREAM_* environment flag.
// ---------------------------------------------------------------------------
await RunStreamSamplesAsync(client, classCode, ticker);

return 0;

// ===========================================================================
// HTTP examples
// ===========================================================================

static async Task ShowLimitsAsync(BcsInvestApiClient client)
{
    var limits = await client.Limits.GetLimitsAsync();
    Console.WriteLine(
        $"Limits: depo={limits.DepoLimit.Count}, futureHoldings={limits.FutureHolding.Count}, " +
        $"money={limits.MoneyLimits.Count}, futures={limits.FuturesLimits.Count}");
}

static async Task ShowPortfolioAsync(BcsInvestApiClient client)
{
    var portfolio = await client.Portfolio.GetPortfolioAsync();
    Console.WriteLine($"Portfolio positions: {portfolio.Count}");
}

static async Task ShowInstrumentsAsync(BcsInvestApiClient client)
{
    var byIsin = await client.Instruments.GetInstrumentsByIsinsAsync(GetConfiguredIsins(), page: 0, size: 50);
    Console.WriteLine($"Instruments by ISIN (page 0): {byIsin.Count}");
    foreach (var instrument in byIsin.Take(5))
    {
        Console.WriteLine($"  {instrument.Isin}: {instrument.Ticker} {instrument.PrimaryBoard} {instrument.DisplayName}");
    }

    var byTicker = await client.Instruments.GetInstrumentsByTickersAsync(GetConfiguredTickers(), page: 0, size: 50);
    Console.WriteLine($"Instruments by ticker (page 0): {byTicker.Count}");
    foreach (var instrument in byTicker.Take(5))
    {
        Console.WriteLine($"  {instrument.Ticker}: {instrument.Isin} {instrument.PrimaryBoard} {instrument.DisplayName}");
    }

    var stocks = await client.Instruments.GetInstrumentsByTypeAsync(BcsInstrumentTypes.Stock, page: 0, size: 10);
    Console.WriteLine($"Instruments by type STOCK (page 0): {stocks.Count}");
    foreach (var instrument in stocks.Take(5))
    {
        Console.WriteLine($"  {instrument.Ticker}: {instrument.Isin} {instrument.PrimaryBoard} {instrument.DisplayName}");
    }
}

static async Task ShowCandlesAsync(BcsInvestApiClient client, string classCode, string ticker)
{
    var end = DateTimeOffset.UtcNow;
    var start = end.AddDays(-1);
    var candles = await client.MarketData.GetCandlesAsync(classCode, ticker, start, end, BcsCandleTimeFrames.Minute1);
    Console.WriteLine($"Minute1 candles for {classCode}/{ticker}: {candles.Bars.Count}");
    foreach (var bar in candles.Bars.Take(5))
    {
        Console.WriteLine($"  {bar.Time:O}: O={bar.Open} H={bar.High} L={bar.Low} C={bar.Close} V={bar.Volume}");
    }
}

static async Task ShowTradingScheduleAsync(BcsInvestApiClient client, string classCode, string ticker)
{
    var schedule = await client.TradingSchedule.GetDailyTradingScheduleAsync(classCode, ticker);
    Console.WriteLine($"Daily schedule for {classCode}/{ticker}: workDay={schedule.IsWorkDay}, intervals={schedule.DailySchedule.Count}");
    foreach (var interval in schedule.DailySchedule)
    {
        Console.WriteLine(
            $"  {interval.StartDate:HH:mm:ss}-{interval.EndDate:HH:mm:ss}: {interval.TradingSessionType} ({interval.TradingSessionStatus})");
    }

    var status = await client.TradingSchedule.GetTradingScheduleStatusAsync(classCode);
    Console.WriteLine(
        $"Current session for {classCode}: {status.TradingSessionType} ({status.TradingSessionStatus}), next={status.NextSessionDate:O}");
}

static async Task ShowOrdersAsync(BcsInvestApiClient client, string classCode, string ticker)
{
    var end = DateTimeOffset.UtcNow;
    var start = end.AddDays(-500);
    var orders = await client.Orders.SearchOrdersAsync(
        new BcsOrdersSearchRequest { StartDateTime = start, EndDateTime = end },
        page: 0,
        size: 10,
        sort: new[] { BcsOrderSort.OrderDateTimeDesc });

    Console.WriteLine(
        $"Orders for {classCode}/{ticker}: pageRecords={orders.Records.Count}, total={orders.TotalRecords}, pages={orders.TotalPages}");
    foreach (var order in orders.Records.Take(5))
    {
        Console.WriteLine(
            $"  {order.OrderDateTime:O}: #{order.OrderNum} {order.Side} {order.OrderType} {order.OrderStatus} " +
            $"{order.Ticker}/{order.ClassCode} qty={order.OrderQuantity} price={order.Price}");
    }
}

static async Task ShowTradesAsync(BcsInvestApiClient client)
{
    var end = DateTimeOffset.UtcNow;
    var start = end.AddDays(-500);
    var trades = await client.Trades.SearchTradesAsync(
        new BcsTradesSearchRequest { StartDateTime = start, EndDateTime = end },
        page: 0,
        size: 10,
        sort: new[] { BcsTradeSort.TradeDateTimeDesc });

    Console.WriteLine($"Trades: pageRecords={trades.Records.Count}, total={trades.TotalRecords}, pages={trades.TotalPages}");
    foreach (var trade in trades.Records.Take(5))
    {
        Console.WriteLine(
            $"  {trade.TradeDateTime:O}: #{trade.TradeNum} {trade.Side} {trade.Ticker}/{trade.ClassCode} " +
            $"qty={trade.TradeQuantity} price={trade.Price} amount={trade.ContractAmount}");
    }
}

static async Task ShowMarginDiscountsAsync(BcsInvestApiClient client)
{
    var discounts = await client.MarginalIndicators.GetInstrumentsDiscountsAsync();
    Console.WriteLine($"Instrument discounts: {discounts.Count}");
    foreach (var discount in discounts.Take(5))
    {
        Console.WriteLine($"  {discount.Ticker}: long={discount.DiscountLong} short={discount.DiscountShort}");
    }
}

static async Task ShowOrderStatusAsync(BcsInvestApiClient client, Guid? orderStatusClientOrderId)
{
    if (orderStatusClientOrderId is not { } statusId)
    {
        Console.WriteLine("Order status skipped. Set BCS_SAMPLE_ORDER_STATUS_CLIENT_ORDER_ID to enable it.");
        return;
    }

    var status = await client.Orders.GetOrderStatusAsync(statusId);
    Console.WriteLine(
        $"Order status: clientOrderId={status.ClientOrderId}, originalClientOrderId={status.OriginalClientOrderId}");
    if (status.Data is { } data)
    {
        Console.WriteLine(
            $"  {data.TransactionTime:O}: {data.Ticker}/{data.ClassCode} status={data.OrderStatus} " +
            $"execType={data.ExecutionType} qty={data.OrderQuantity} remained={data.RemainedQuantity} price={data.Price}");
    }
}

static async Task ShowCreateOrderAsync(BcsInvestApiClient client, BcsCreateOrderRequest? createOrderRequest)
{
    if (createOrderRequest is null)
    {
        Console.WriteLine("Order create skipped. Set BCS_SAMPLE_CREATE_ORDER=true, BCS_SAMPLE_CREATE_ORDER_QUANTITY, and BCS_SAMPLE_CREATE_PRICE to enable it.");
        return;
    }

    var response = await client.Orders.CreateOrderAsync(createOrderRequest);
    Console.WriteLine($"Create order result: clientOrderId={response.ClientOrderId}, status={response.Status}");
}

static async Task ShowUpdateOrderAsync(BcsInvestApiClient client, (Guid OriginalId, BcsUpdateOrderRequest Request)? updateOrderConfig)
{
    if (updateOrderConfig is not { } config)
    {
        Console.WriteLine("Order update skipped. Set BCS_SAMPLE_UPDATE_ORIGINAL_CLIENT_ORDER_ID, BCS_SAMPLE_UPDATE_ORDER_QUANTITY, and BCS_SAMPLE_UPDATE_PRICE to enable it.");
        return;
    }

    var response = await client.Orders.UpdateOrderAsync(config.OriginalId, config.Request);
    Console.WriteLine($"Update order result: clientOrderId={response.ClientOrderId}, status={response.Status}");
}

static async Task ShowCancelOrderAsync(BcsInvestApiClient client, (Guid OriginalId, Guid ClientOrderId)? cancelOrderConfig)
{
    if (cancelOrderConfig is not { } config)
    {
        Console.WriteLine("Order cancel skipped. Set BCS_SAMPLE_CANCEL_ORIGINAL_CLIENT_ORDER_ID to enable it.");
        return;
    }

    var response = await client.Orders.CancelOrderAsync(config.OriginalId, config.ClientOrderId);
    Console.WriteLine($"Cancel order result: clientOrderId={response.ClientOrderId}, status={response.Status}");
}

// ===========================================================================
// WebSocket examples
// ===========================================================================

static async Task RunStreamSamplesAsync(BcsInvestApiClient client, string classCode, string ticker)
{
    var instrument = new[] { new BcsMarketDataInstrument { ClassCode = classCode, Ticker = ticker } };

    await RunIfEnabledAsync("BCS_SAMPLE_STREAM_CANDLES", "last-candle (M1)", () =>
    {
        var subscribe = new BcsMarketDataSubscribeRequest { DataType = BcsMarketDataType.Candles, TimeFrame = "M1", Instruments = instrument };
        return RunMarketDataStreamAsync(
            client,
            "last-candle (M1)",
            maxMessages: 5,
            subscribe,
            subscribe with { SubscribeType = BcsMarketDataSubscribeType.Unsubscribe },
            (BcsLastCandleMessage message) => message.Errors,
            message => string.Equals(message.ResponseType, "CandleStickSuccess", StringComparison.Ordinal),
            message => $"{message.DateTime:O} {message.Ticker}/{message.ClassCode}: O={message.Open} H={message.High} L={message.Low} C={message.Close} V={message.Volume}");
    });

    await RunIfEnabledAsync("BCS_SAMPLE_STREAM_ORDERBOOK", "order-book (depth 20)", () =>
    {
        var subscribe = new BcsOrderBookSubscribeRequest { DataType = BcsMarketDataType.OrderBook, Depth = 20, Instruments = instrument };
        return RunMarketDataStreamAsync(
            client,
            "order-book (depth 20)",
            maxMessages: 5,
            subscribe,
            subscribe with { SubscribeType = BcsMarketDataSubscribeType.Unsubscribe },
            (BcsOrderBookMessage message) => message.Errors,
            message => string.Equals(message.ResponseType, "OrderBookSuccess", StringComparison.Ordinal),
            message =>
            {
                var bestBid = message.Bids.Count > 0 ? message.Bids[0] : null;
                var bestAsk = message.Asks.Count > 0 ? message.Asks[0] : null;
                return $"{message.DateTime:O} {message.Ticker}/{message.ClassCode}: " +
                       $"bestBid={bestBid?.Price}x{bestBid?.Quantity} bestAsk={bestAsk?.Price}x{bestAsk?.Quantity} " +
                       $"levels={message.Bids.Count}/{message.Asks.Count}";
            });
    });

    await RunIfEnabledAsync("BCS_SAMPLE_STREAM_QUOTES", "quotes", () =>
    {
        var subscribe = new BcsQuotesSubscribeRequest { DataType = BcsMarketDataType.Quotes, Instruments = instrument };
        return RunMarketDataStreamAsync(
            client,
            "quotes",
            maxMessages: 5,
            subscribe,
            subscribe with { SubscribeType = BcsMarketDataSubscribeType.Unsubscribe },
            (BcsQuotesMessage message) => message.Errors,
            _ => false,
            message => $"{message.DateTime:O} {message.Ticker}/{message.ClassCode}: last={message.Last} bid={message.Bid} offer={message.Offer} status={message.SecurityTradingStatus}");
    });

    await RunIfEnabledAsync("BCS_SAMPLE_STREAM_TRADES", "anonymous-trades", () =>
    {
        var subscribe = new BcsLastTradesSubscribeRequest { DataType = BcsMarketDataType.AnonymousTrades, Instruments = instrument };
        return RunMarketDataStreamAsync(
            client,
            "anonymous-trades",
            maxMessages: 5,
            subscribe,
            subscribe with { SubscribeType = BcsMarketDataSubscribeType.Unsubscribe },
            (BcsLastTradeMessage message) => message.Errors,
            message => string.Equals(message.ResponseType, "LastTradesSuccess", StringComparison.Ordinal),
            message => $"{message.DateTime:O} {message.Ticker}/{message.ClassCode}: {message.Side} price={message.Price} qty={message.Quantity} volume={message.Volume}");
    });

    await RunIfEnabledAsync("BCS_SAMPLE_STREAM_LIMITS", "limits", () =>
        RunPushStreamAsync<BcsLimitsResponse>(
            client,
            BcsWebSocketEndpoints.Limits,
            "limits",
            maxMessages: 3,
            message => $"depo={message.DepoLimit.Count}, futureHoldings={message.FutureHolding.Count}, money={message.MoneyLimits.Count}, futures={message.FuturesLimits.Count}"));

    await RunIfEnabledAsync("BCS_SAMPLE_STREAM_PORTFOLIO", "portfolio", () =>
        RunPushStreamAsync<BcsPortfolioItem[]>(
            client,
            BcsWebSocketEndpoints.Portfolio,
            "portfolio",
            maxMessages: 3,
            positions => $"{positions.Length} positions"));

    await RunIfEnabledAsync("BCS_SAMPLE_STREAM_ORDER_EXECUTIONS", "order-execution", () =>
        RunPushStreamAsync<BcsOrderStatusResponse>(
            client,
            BcsWebSocketEndpoints.OrderExecutions,
            "order-execution",
            maxMessages: 5,
            update => $"clientOrderId={update.ClientOrderId} original={update.OriginalClientOrderId} status={update.Data?.OrderStatus}"));

    await RunIfEnabledAsync("BCS_SAMPLE_STREAM_ORDER_TRANSACTIONS", "order-transaction", () =>
        RunPushStreamAsync<BcsOrderStatusResponse>(
            client,
            BcsWebSocketEndpoints.OrderTransactions,
            "order-transaction",
            maxMessages: 5,
            update => $"clientOrderId={update.ClientOrderId} original={update.OriginalClientOrderId} status={update.Data?.OrderStatus}"));

    await RunIfEnabledAsync("BCS_SAMPLE_STREAM_MARGIN", "marginal-indicators", () =>
        RunPushStreamAsync<BcsMarginalIndicators>(
            client,
            BcsWebSocketEndpoints.MarginalIndicators,
            "marginal-indicators",
            maxMessages: 3,
            snapshot => $"Account {snapshot.AgreementData?.Number} ({snapshot.AgreementData?.DisplayName}): " +
                        $"value={snapshot.PortfolioCurrentValue?.CurrentValueRub}₽ marginOn={snapshot.PortfolioStability?.IsMarginOn} " +
                        $"status={snapshot.PortfolioStability?.StabilityStatusName}"));
}

// Market-data stream: connect + subscribe in one call, read up to maxMessages, then unsubscribe.
static async Task RunMarketDataStreamAsync<TSubscribe, TMessage>(
    BcsInvestApiClient client,
    string label,
    int maxMessages,
    TSubscribe subscribe,
    TSubscribe unsubscribe,
    Func<TMessage, IReadOnlyList<BcsMarketDataError>> getErrors,
    Func<TMessage, bool> isAck,
    Func<TMessage, string> describe)
    where TSubscribe : notnull
    where TMessage : class
{
    Console.WriteLine($"Connecting to {label} stream...");

    // The SDK provides connect + subscribe + receive plumbing only; the caller owns the socket lifecycle.
    using var socket = await client.WebSockets.ConnectAndSubscribeAsync(BcsWebSocketEndpoints.MarketData, subscribe);
    Console.WriteLine($"Subscribed; reading up to {maxMessages} {label} messages (30s timeout)...");

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    try
    {
        var received = 0;
        await foreach (var message in socket.ReceiveJsonMessagesAsync<TMessage>(cts.Token))
        {
            var errors = getErrors(message);
            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    Console.WriteLine($"  Error [{error.Code}]: {error.Message}");
                }

                break;
            }

            if (isAck(message))
            {
                Console.WriteLine("  Subscription confirmed.");
                continue;
            }

            Console.WriteLine($"  {describe(message)}");
            if (++received >= maxMessages)
            {
                break;
            }
        }

        // Politely unsubscribe with the same payload shape.
        await socket.SendJsonMessageAsync(unsubscribe);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine($"  Timed out waiting for {label} messages.");
    }
}

// Push stream: connect and read up to maxMessages; no subscribe message is required.
static async Task RunPushStreamAsync<TMessage>(
    BcsInvestApiClient client,
    Uri endpoint,
    string label,
    int maxMessages,
    Func<TMessage, string> describe)
    where TMessage : class
{
    Console.WriteLine($"Connecting to {label} stream...");

    using var socket = await client.WebSockets.ConnectAsync(endpoint);
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    try
    {
        var received = 0;
        await foreach (var message in socket.ReceiveJsonMessagesAsync<TMessage>(cts.Token))
        {
            Console.WriteLine($"  {describe(message)}");
            if (++received >= maxMessages)
            {
                break;
            }
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine($"  Timed out waiting for {label} updates.");
    }
}

static async Task RunIfEnabledAsync(string variableName, string label, Func<Task> run)
{
    if (IsEnabled(variableName))
    {
        await run();
    }
    else
    {
        Console.WriteLine($"{label} stream skipped. Set {variableName}=true to enable it.");
    }
}

// ===========================================================================
// Configuration parsing helpers
// ===========================================================================

static BcsAuthClientIds GetConfiguredClientId(bool writeOperationEnabled)
{
    var rawClientId = Environment.GetEnvironmentVariable("BCS_SAMPLE_CLIENT_ID");
    if (string.IsNullOrWhiteSpace(rawClientId))
    {
        return writeOperationEnabled ? BcsAuthClientIds.TradeApiWrite : BcsAuthClientIds.TradeApiRead;
    }

    return rawClientId.Trim().ToLowerInvariant() switch
    {
        "read" or "trade-api-read" => BcsAuthClientIds.TradeApiRead,
        "write" or "trade-api-write" => BcsAuthClientIds.TradeApiWrite,
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

    return (originalId.Value, ParseGuidWithDefault("BCS_SAMPLE_CANCEL_CLIENT_ORDER_ID", Guid.NewGuid));
}

static string GetEnvOrDefault(string variableName, string defaultValue)
{
    var rawValue = Environment.GetEnvironmentVariable(variableName);
    return string.IsNullOrWhiteSpace(rawValue) ? defaultValue : rawValue;
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
        _ => throw new InvalidOperationException($"{variableName} must be one of: 1/0, true/false, yes/no, on/off."),
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

static string[] GetConfiguredIsins() =>
    SplitOrDefault("BCS_SAMPLE_ISINS", new[] { "RU0009029540", "RU0007661625", "RU000A0J2Q06" });

static string[] GetConfiguredTickers() =>
    SplitOrDefault("BCS_SAMPLE_TICKERS", new[] { "SBER", "GAZP", "ROSN" });

static string[] SplitOrDefault(string variableName, string[] defaultValue)
{
    var rawValue = Environment.GetEnvironmentVariable(variableName);
    return string.IsNullOrWhiteSpace(rawValue)
        ? defaultValue
        : rawValue.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

using System.Globalization;
using Bcs.InvestApi;
using Bcs.InvestApi.Contracts.Enums;
using Bcs.InvestApi.Contracts.Orders;

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

var limits = await client.GetLimitsAsync();
Console.WriteLine($"Depo limits: {limits.DepoLimit.Count}");
Console.WriteLine($"Future holdings: {limits.FutureHolding.Count}");
Console.WriteLine($"Money limits: {limits.MoneyLimits.Count}");
Console.WriteLine($"Futures limits: {limits.FuturesLimits.Count}");

var portfolio = await client.GetPortfolioAsync();
Console.WriteLine($"Portfolio positions: {portfolio.Count}");

var isins = GetConfiguredIsins();
var instruments = await client.GetInstrumentsByIsinsAsync(isins, page: 0, size: 50);
Console.WriteLine($"Instruments by ISIN page 0: {instruments.Count}");

foreach (var instrument in instruments.Take(10))
{
    Console.WriteLine(
        $"  {instrument.Isin}: {instrument.Ticker} {instrument.PrimaryBoard} {instrument.DisplayName}");
}

var tickers = GetConfiguredTickers();
var instrumentsByTicker = await client.GetInstrumentsByTickersAsync(tickers, page: 0, size: 50);
Console.WriteLine($"Instruments by ticker page 0: {instrumentsByTicker.Count}");

foreach (var instrument in instrumentsByTicker.Take(10))
{
    Console.WriteLine(
        $"  {instrument.Ticker}: {instrument.Isin} {instrument.PrimaryBoard} {instrument.DisplayName}");
}

var stocksPage = await client.GetInstrumentsByTypeAsync(
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
var candles = await client.GetCandlesAsync(
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

//var schedule = await client.GetDailyTradingScheduleAsync(classCode, ticker);
//Console.WriteLine($"Daily schedule for {classCode}/{ticker}: work day = {schedule.IsWorkDay}, intervals = {schedule.DailySchedule.Count}");

//foreach (var interval in schedule.DailySchedule)
//{
//    Console.WriteLine(
//        $"  {interval.StartDate:HH:mm:ss}-{interval.EndDate:HH:mm:ss}: {interval.TradingSessionType} ({interval.TradingSessionStatus})");
//}

var ordersEnd = DateTimeOffset.UtcNow;
var ordersStart = ordersEnd.AddDays(-500);
var orders = await client.SearchOrdersAsync(
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
    var statusResponse = await client.GetOrderStatusAsync(statusId);
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
    var createResponse = await client.CreateOrderAsync(createOrderRequest);
    Console.WriteLine(
        $"Create order result: clientOrderId = {createResponse.ClientOrderId}, status = {createResponse.Status}");
}
else
{
    Console.WriteLine("Order create skipped. Set BCS_SAMPLE_CREATE_ORDER=true, BCS_SAMPLE_CREATE_ORDER_QUANTITY, and BCS_SAMPLE_CREATE_PRICE to enable it.");
}

if (updateOrderConfig is { } updateCfg)
{
    var updateResponse = await client.UpdateOrderAsync(updateCfg.OriginalId, updateCfg.Request);
    Console.WriteLine(
        $"Update order result: clientOrderId = {updateResponse.ClientOrderId}, status = {updateResponse.Status}");
}
else
{
    Console.WriteLine("Order update skipped. Set BCS_SAMPLE_UPDATE_ORIGINAL_CLIENT_ORDER_ID, BCS_SAMPLE_UPDATE_ORDER_QUANTITY, and BCS_SAMPLE_UPDATE_PRICE to enable it.");
}

if (cancelOrderConfig is { } cancelCfg)
{
    var cancelResponse = await client.CancelOrderAsync(cancelCfg.OriginalId, cancelCfg.ClientOrderId);
    Console.WriteLine($"Cancel order result: clientOrderId = {cancelResponse.ClientOrderId}, status = {cancelResponse.Status}");
}
else
{
    Console.WriteLine("Order cancel skipped. Set BCS_SAMPLE_CANCEL_ORIGINAL_CLIENT_ORDER_ID to enable it.");
}

return 0;

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

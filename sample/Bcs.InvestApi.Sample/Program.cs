using Bcs.InvestApi;
using Bcs.InvestApi.Contracts.Enums;
using Bcs.InvestApi.Contracts.Orders;

var refreshToken = Environment.GetEnvironmentVariable("BCS_REFRESH_TOKEN");
if (string.IsNullOrWhiteSpace(refreshToken))
{
    Console.Error.WriteLine("Set BCS_REFRESH_TOKEN environment variable to your stable refresh/bootstrap secret.");
    return 1;
}

var cancelOriginalClientOrderIdValue = Environment.GetEnvironmentVariable("BCS_SAMPLE_CANCEL_ORIGINAL_CLIENT_ORDER_ID");
var clientId = GetConfiguredClientId(cancelOriginalClientOrderIdValue);

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

if (!string.IsNullOrWhiteSpace(cancelOriginalClientOrderIdValue))
{
    if (!Guid.TryParse(cancelOriginalClientOrderIdValue, out var cancelOriginalClientOrderId))
    {
        Console.Error.WriteLine("BCS_SAMPLE_CANCEL_ORIGINAL_CLIENT_ORDER_ID must be a valid UUID.");
        return 1;
    }

    var cancelClientOrderId = GetConfiguredCancelClientOrderId();
    var cancelResponse = await client.CancelOrderAsync(cancelOriginalClientOrderId, cancelClientOrderId);
    Console.WriteLine($"Cancel order result: clientOrderId = {cancelResponse.ClientOrderId}, status = {cancelResponse.Status}");
}
else
{
    Console.WriteLine("Order cancel skipped. Set BCS_SAMPLE_CANCEL_ORIGINAL_CLIENT_ORDER_ID to enable it.");
}

return 0;

static BcsAuthClientIds GetConfiguredClientId(string? cancelOriginalClientOrderIdValue)
{
    var rawClientId = Environment.GetEnvironmentVariable("BCS_SAMPLE_CLIENT_ID");
    if (string.IsNullOrWhiteSpace(rawClientId))
    {
        return string.IsNullOrWhiteSpace(cancelOriginalClientOrderIdValue)
            ? BcsAuthClientIds.TradeApiRead
            : BcsAuthClientIds.TradeApiWrite;
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

static Guid GetConfiguredCancelClientOrderId()
{
    var rawClientOrderId = Environment.GetEnvironmentVariable("BCS_SAMPLE_CANCEL_CLIENT_ORDER_ID");
    if (string.IsNullOrWhiteSpace(rawClientOrderId))
    {
        return Guid.NewGuid();
    }

    if (!Guid.TryParse(rawClientOrderId, out var clientOrderId))
    {
        throw new InvalidOperationException("BCS_SAMPLE_CANCEL_CLIENT_ORDER_ID must be a valid UUID.");
    }

    return clientOrderId;
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

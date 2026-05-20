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

internal static class Program
{
    private static readonly IReadOnlyList<SampleCommand> Commands =
    [
        new("smoke", "Run a small read-only smoke flow.", RunSmokeAsync),
        new("limits", "Call Limits.GetLimitsAsync.", ShowLimitsAsync),
        new("portfolio", "Call Portfolio.GetPortfolioAsync.", ShowPortfolioAsync),
        new("instruments-by-isin", "Call Instruments.GetInstrumentsByIsinsAsync.", ShowInstrumentsByIsinsAsync),
        new("instruments-by-ticker", "Call Instruments.GetInstrumentsByTickersAsync.", ShowInstrumentsByTickersAsync),
        new("instruments-by-type", "Call Instruments.GetInstrumentsByTypeAsync.", ShowInstrumentsByTypeAsync),
        new("candles", "Call MarketData.GetCandlesAsync.", ShowCandlesAsync),
        new("schedule", "Call TradingSchedule.GetDailyTradingScheduleAsync.", ShowDailyScheduleAsync),
        new("schedule-status", "Call TradingSchedule.GetTradingScheduleStatusAsync.", ShowScheduleStatusAsync),
        new("orders", "Call Orders.SearchOrdersAsync.", ShowOrdersAsync),
        new("order-status", "Call Orders.GetOrderStatusAsync.", ShowOrderStatusAsync),
        new("trades", "Call Trades.SearchTradesAsync.", ShowTradesAsync),
        new("margin-discounts", "Call MarginalIndicators.GetInstrumentsDiscountsAsync.", ShowMarginDiscountsAsync),
        new("order-create", "Call Orders.CreateOrderAsync. Requires trade-api-write.", CreateOrderAsync, RequiresWriteClient: true),
        new("order-update", "Call Orders.UpdateOrderAsync. Requires trade-api-write.", UpdateOrderAsync, RequiresWriteClient: true),
        new("order-cancel", "Call Orders.CancelOrderAsync. Requires trade-api-write.", CancelOrderAsync, RequiresWriteClient: true),
        new("stream-candles", "Connect to the last-candle WebSocket stream.", StreamLastCandlesAsync),
        new("stream-orderbook", "Connect to the order-book WebSocket stream.", StreamOrderBookAsync),
        new("stream-quotes", "Connect to the quotes WebSocket stream.", StreamQuotesAsync),
        new("stream-trades", "Connect to the anonymous-trades WebSocket stream.", StreamLastTradesAsync),
        new("stream-limits", "Connect to the limits WebSocket stream.", StreamLimitsAsync),
        new("stream-portfolio", "Connect to the portfolio WebSocket stream.", StreamPortfolioAsync),
        new("stream-order-executions", "Connect to the order execution WebSocket stream.", StreamOrderExecutionsAsync),
        new("stream-order-transactions", "Connect to the order transaction WebSocket stream.", StreamOrderTransactionsAsync),
        new("stream-margin", "Connect to the marginal indicators WebSocket stream.", StreamMarginalIndicatorsAsync),
    ];

    private static readonly IReadOnlyDictionary<string, SampleCommand> CommandsByName =
        Commands.ToDictionary(command => command.Name, StringComparer.OrdinalIgnoreCase);

    public static async Task<int> Main(string[] args)
    {
        var commandName = ResolveCommandName(args);
        if (string.IsNullOrWhiteSpace(commandName) || IsHelp(commandName))
        {
            PrintUsage();
            return 0;
        }

        if (!CommandsByName.TryGetValue(commandName, out var command))
        {
            Console.Error.WriteLine($"Unknown sample command: {commandName}");
            Console.Error.WriteLine();
            PrintUsage();
            return 1;
        }

        try
        {
            var refreshToken = GetRequiredString("BCS_REFRESH_TOKEN");
            var clientId = GetConfiguredClientId(command.RequiresWriteClient);

            await using var client = BcsInvestApiClientFactory.Create(
                refreshToken: refreshToken,
                clientId: clientId);

            var context = CreateContext(client);
            await command.ExecuteAsync(context, CancellationToken.None).ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    private static string? ResolveCommandName(string[] args)
    {
        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        {
            return args[0].Trim();
        }

        return GetOptionalString("BCS_SAMPLE_COMMAND")
            ?? GetOptionalString("BCS_SAMPLE_ENDPOINT");
    }

    private static bool IsHelp(string commandName) =>
        commandName is "-h" or "--help" or "help";

    private static SampleContext CreateContext(BcsInvestApiClient client)
    {
        var classCode = GetStringOrDefault("BCS_SAMPLE_CLASS_CODE", "TQBR");
        var ticker = GetStringOrDefault("BCS_SAMPLE_TICKER", "SBER");

        return new SampleContext(
            client,
            classCode,
            ticker,
            SplitStringList("BCS_SAMPLE_ISINS", ["RU0009029540", "RU0007661625", "RU000A0J2Q06"]),
            SplitStringList("BCS_SAMPLE_TICKERS", ["SBER", "GAZP", "ROSN"]),
            ParseIntOrDefault("BCS_SAMPLE_PAGE", 0, minValue: 0),
            ParseIntOrDefault("BCS_SAMPLE_SIZE", 10, minValue: 1),
            ParseIntOrDefault("BCS_SAMPLE_STREAM_MESSAGES", 5, minValue: 1),
            TimeSpan.FromSeconds(ParseIntOrDefault("BCS_SAMPLE_STREAM_TIMEOUT_SECONDS", 30, minValue: 1)));
    }

    private static void PrintUsage()
    {
        Console.WriteLine("BCS Invest API sample");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project sample/Bcs.InvestApi.Sample -- <command>");
        Console.WriteLine();
        Console.WriteLine("Required:");
        Console.WriteLine("  BCS_REFRESH_TOKEN - external refresh/bootstrap secret.");
        Console.WriteLine();
        Console.WriteLine("Common optional variables:");
        Console.WriteLine("  BCS_SAMPLE_CLASS_CODE=TQBR");
        Console.WriteLine("  BCS_SAMPLE_TICKER=SBER");
        Console.WriteLine("  BCS_SAMPLE_ISINS=RU0009029540,RU0007661625,RU000A0J2Q06");
        Console.WriteLine("  BCS_SAMPLE_TICKERS=SBER,GAZP,ROSN");
        Console.WriteLine("  BCS_SAMPLE_PAGE=0");
        Console.WriteLine("  BCS_SAMPLE_SIZE=10");
        Console.WriteLine("  BCS_SAMPLE_CLIENT_ID=read|write");
        Console.WriteLine();
        Console.WriteLine("Commands:");

        foreach (var command in Commands)
        {
            Console.WriteLine($"  {command.Name,-27} {command.Description}");
        }
    }

    private static async Task RunSmokeAsync(SampleContext context, CancellationToken cancellationToken)
    {
        await ShowLimitsAsync(context, cancellationToken).ConfigureAwait(false);
        await ShowPortfolioAsync(context, cancellationToken).ConfigureAwait(false);
        await ShowInstrumentsByTickersAsync(context, cancellationToken).ConfigureAwait(false);
        await ShowCandlesAsync(context, cancellationToken).ConfigureAwait(false);
        await ShowDailyScheduleAsync(context, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ShowLimitsAsync(SampleContext context, CancellationToken cancellationToken)
    {
        var limits = await context.Client.Limits.GetLimitsAsync(cancellationToken).ConfigureAwait(false);
        PrintLimits(limits);
    }

    private static void PrintLimits(BcsLimitsResponse limits)
    {
        Console.WriteLine("Limits:");
        Console.WriteLine($"  Depo limits: {limits.DepoLimit.Count}");
        Console.WriteLine($"  Future holdings: {limits.FutureHolding.Count}");
        Console.WriteLine($"  Money limits: {limits.MoneyLimits.Count}");
        Console.WriteLine($"  Futures limits: {limits.FuturesLimits.Count}");
    }

    private static async Task ShowPortfolioAsync(SampleContext context, CancellationToken cancellationToken)
    {
        var portfolio = await context.Client.Portfolio.GetPortfolioAsync(cancellationToken).ConfigureAwait(false);

        Console.WriteLine($"Portfolio positions: {portfolio.Count}");
        foreach (var position in portfolio.Take(10))
        {
            PrintPortfolioItem(position, prefix: "  ");
        }
    }

    private static void PrintPortfolioItem(BcsPortfolioItem position, string prefix)
    {
        Console.WriteLine(
            $"{prefix}{position.Ticker}/{position.Board}: qty={position.Quantity} " +
            $"current={position.CurrentValue} pl={position.UnrealizedPL}");
    }

    private static async Task ShowInstrumentsByIsinsAsync(SampleContext context, CancellationToken cancellationToken)
    {
        var instruments = await context.Client.Instruments
            .GetInstrumentsByIsinsAsync(context.Isins, context.Page, context.Size, cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine($"Instruments by ISIN: {instruments.Count}");
        PrintInstruments(instruments);
    }

    private static async Task ShowInstrumentsByTickersAsync(SampleContext context, CancellationToken cancellationToken)
    {
        var instruments = await context.Client.Instruments
            .GetInstrumentsByTickersAsync(context.Tickers, context.Page, context.Size, cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine($"Instruments by ticker: {instruments.Count}");
        PrintInstruments(instruments);
    }

    private static async Task ShowInstrumentsByTypeAsync(SampleContext context, CancellationToken cancellationToken)
    {
        var type = ParseEnumOrDefault("BCS_SAMPLE_INSTRUMENT_TYPE", BcsInstrumentTypes.Stock);
        var baseAssetTicker = GetOptionalString("BCS_SAMPLE_BASE_ASSET_TICKER");

        var instruments = await context.Client.Instruments
            .GetInstrumentsByTypeAsync(type, context.Page, context.Size, baseAssetTicker, cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine($"Instruments by type {type}: {instruments.Count}");
        PrintInstruments(instruments);
    }

    private static void PrintInstruments(IEnumerable<Bcs.InvestApi.Contracts.Instruments.BcsInstrument> instruments)
    {
        foreach (var instrument in instruments.Take(10))
        {
            Console.WriteLine(
                $"  {instrument.Ticker}: isin={instrument.Isin} board={instrument.PrimaryBoard} " +
                $"name={instrument.DisplayName}");
        }
    }

    private static async Task ShowCandlesAsync(SampleContext context, CancellationToken cancellationToken)
    {
        var endDate = ParseDateTimeOffsetOrDefault("BCS_SAMPLE_CANDLES_TO", DateTimeOffset.UtcNow);
        var startDate = ParseDateTimeOffsetOrDefault("BCS_SAMPLE_CANDLES_FROM", endDate.AddDays(-1));
        var timeFrame = ParseEnumOrDefault("BCS_SAMPLE_CANDLE_TIMEFRAME", BcsCandleTimeFrames.Minute1);

        var candles = await context.Client.MarketData
            .GetCandlesAsync(context.ClassCode, context.Ticker, startDate, endDate, timeFrame, cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine(
            $"Candles for {context.ClassCode}/{context.Ticker} {timeFrame}: {candles.Bars.Count}");

        foreach (var bar in candles.Bars.Take(10))
        {
            Console.WriteLine(
                $"  {bar.Time:O}: O={bar.Open} H={bar.High} L={bar.Low} C={bar.Close} V={bar.Volume}");
        }
    }

    private static async Task ShowDailyScheduleAsync(SampleContext context, CancellationToken cancellationToken)
    {
        var schedule = await context.Client.TradingSchedule
            .GetDailyTradingScheduleAsync(context.ClassCode, context.Ticker, cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine(
            $"Daily schedule for {context.ClassCode}/{context.Ticker}: " +
            $"workDay={schedule.IsWorkDay}, intervals={schedule.DailySchedule.Count}");

        foreach (var interval in schedule.DailySchedule)
        {
            Console.WriteLine(
                $"  {FormatTime(interval.StartDate)}-{FormatTime(interval.EndDate)}: " +
                $"{interval.TradingSessionType} ({interval.TradingSessionStatus})");
        }
    }

    private static async Task ShowScheduleStatusAsync(SampleContext context, CancellationToken cancellationToken)
    {
        var status = await context.Client.TradingSchedule
            .GetTradingScheduleStatusAsync(context.ClassCode, cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine($"Trading schedule status for {context.ClassCode}:");
        Console.WriteLine($"  typeId={status.TradingSessionTypeId}");
        Console.WriteLine($"  type={status.TradingSessionType}");
        Console.WriteLine($"  status={status.TradingSessionStatus}");
        Console.WriteLine($"  nextSession={status.NextSessionDate:O}");
    }

    private static async Task ShowOrdersAsync(SampleContext context, CancellationToken cancellationToken)
    {
        var endDate = ParseDateTimeOffsetOrDefault("BCS_SAMPLE_SEARCH_TO", DateTimeOffset.UtcNow);
        var startDate = ParseDateTimeOffsetOrDefault("BCS_SAMPLE_SEARCH_FROM", endDate.AddDays(-7));

        var orders = await context.Client.Orders
            .SearchOrdersAsync(
                new BcsOrdersSearchRequest
                {
                    StartDateTime = startDate,
                    EndDateTime = endDate,
                    Tickers = [context.Ticker],
                    ClassCodes = [context.ClassCode],
                },
                context.Page,
                context.Size,
                [BcsOrderSort.OrderDateTimeDesc],
                cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine(
            $"Orders for {context.ClassCode}/{context.Ticker}: records={orders.Records.Count}, " +
            $"total={orders.TotalRecords}, pages={orders.TotalPages}");

        foreach (var order in orders.Records.Take(10))
        {
            Console.WriteLine(
                $"  {order.OrderDateTime:O}: #{order.OrderNum} {order.Side} {order.OrderType} " +
                $"{order.OrderStatus} {order.Ticker}/{order.ClassCode} " +
                $"qty={order.OrderQuantity} price={order.Price}");
        }
    }

    private static async Task ShowOrderStatusAsync(SampleContext context, CancellationToken cancellationToken)
    {
        var originalClientOrderId = ParseRequiredGuid(
            "BCS_SAMPLE_ORIGINAL_CLIENT_ORDER_ID",
            "BCS_SAMPLE_ORDER_STATUS_CLIENT_ORDER_ID");

        var statusResponse = await context.Client.Orders
            .GetOrderStatusAsync(originalClientOrderId, cancellationToken)
            .ConfigureAwait(false);

        PrintOrderStatus(statusResponse);
    }

    private static void PrintOrderStatus(BcsOrderStatusResponse response)
    {
        Console.WriteLine(
            $"Order status: clientOrderId={response.ClientOrderId}, " +
            $"originalClientOrderId={response.OriginalClientOrderId}");

        if (response.Data is null)
        {
            Console.WriteLine("  No order data returned.");
            return;
        }

        var data = response.Data;
        Console.WriteLine(
            $"  {data.TransactionTime:O}: {data.Ticker}/{data.ClassCode} " +
            $"status={data.OrderStatus} execType={data.ExecutionType} " +
            $"qty={data.OrderQuantity} executed={data.ExecutedQuantity} " +
            $"remained={data.RemainedQuantity} price={data.Price}");
    }

    private static async Task ShowTradesAsync(SampleContext context, CancellationToken cancellationToken)
    {
        var endDate = ParseDateTimeOffsetOrDefault("BCS_SAMPLE_SEARCH_TO", DateTimeOffset.UtcNow);
        var startDate = ParseDateTimeOffsetOrDefault("BCS_SAMPLE_SEARCH_FROM", endDate.AddDays(-7));

        var trades = await context.Client.Trades
            .SearchTradesAsync(
                new BcsTradesSearchRequest
                {
                    StartDateTime = startDate,
                    EndDateTime = endDate,
                    Tickers = [context.Ticker],
                    ClassCodes = [context.ClassCode],
                },
                context.Page,
                context.Size,
                [BcsTradeSort.TradeDateTimeDesc],
                cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine(
            $"Trades for {context.ClassCode}/{context.Ticker}: records={trades.Records.Count}, " +
            $"total={trades.TotalRecords}, pages={trades.TotalPages}");

        foreach (var trade in trades.Records.Take(10))
        {
            Console.WriteLine(
                $"  {trade.TradeDateTime:O}: #{trade.TradeNum} order={trade.OrderNum} " +
                $"{trade.Side} {trade.Ticker}/{trade.ClassCode} " +
                $"qty={trade.TradeQuantity} lots={trade.TradeQuantityLots} price={trade.Price}");
        }
    }

    private static async Task ShowMarginDiscountsAsync(SampleContext context, CancellationToken cancellationToken)
    {
        var discounts = await context.Client.MarginalIndicators
            .GetInstrumentsDiscountsAsync(cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine($"Margin instrument discounts: {discounts.Count}");
        foreach (var discount in discounts.Take(10))
        {
            Console.WriteLine(
                $"  {discount.Ticker}: long={discount.DiscountLong}, short={discount.DiscountShort}");
        }
    }

    private static async Task CreateOrderAsync(SampleContext context, CancellationToken cancellationToken)
    {
        var orderType = ParseEnumOrDefault(
            BcsOrderType.Limit,
            "BCS_SAMPLE_ORDER_TYPE",
            "BCS_SAMPLE_CREATE_ORDER_TYPE");
        var price = ParseOptionalPositiveDecimal(
            "BCS_SAMPLE_ORDER_PRICE",
            "BCS_SAMPLE_CREATE_PRICE");

        if (orderType != BcsOrderType.Market && price is null)
        {
            throw new InvalidOperationException(
                "Set BCS_SAMPLE_ORDER_PRICE for non-market order creation.");
        }

        var request = new BcsCreateOrderRequest
        {
            ClientOrderId = ParseGuidWithDefault(
                Guid.NewGuid,
                "BCS_SAMPLE_CLIENT_ORDER_ID",
                "BCS_SAMPLE_CREATE_CLIENT_ORDER_ID"),
            Side = ParseEnumOrDefault(
                BcsOrderSide.Buy,
                "BCS_SAMPLE_ORDER_SIDE",
                "BCS_SAMPLE_CREATE_SIDE"),
            OrderType = orderType,
            OrderQuantity = ParseRequiredPositiveInt64(
                "BCS_SAMPLE_ORDER_QUANTITY",
                "BCS_SAMPLE_CREATE_ORDER_QUANTITY"),
            Price = price,
            Ticker = context.Ticker,
            ClassCode = context.ClassCode,
        };

        var response = await context.Client.Orders.CreateOrderAsync(request, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"Create order: clientOrderId={response.ClientOrderId}, status={response.Status}");
    }

    private static async Task UpdateOrderAsync(SampleContext context, CancellationToken cancellationToken)
    {
        var originalClientOrderId = ParseRequiredGuid(
            "BCS_SAMPLE_ORIGINAL_CLIENT_ORDER_ID",
            "BCS_SAMPLE_UPDATE_ORIGINAL_CLIENT_ORDER_ID");

        var request = new BcsUpdateOrderRequest
        {
            ClientOrderId = ParseGuidWithDefault(
                Guid.NewGuid,
                "BCS_SAMPLE_CLIENT_ORDER_ID",
                "BCS_SAMPLE_UPDATE_CLIENT_ORDER_ID"),
            OrderQuantity = ParseRequiredPositiveInt64(
                "BCS_SAMPLE_ORDER_QUANTITY",
                "BCS_SAMPLE_UPDATE_ORDER_QUANTITY"),
            Price = ParseOptionalPositiveDecimal(
                "BCS_SAMPLE_ORDER_PRICE",
                "BCS_SAMPLE_UPDATE_PRICE"),
        };

        var response = await context.Client.Orders
            .UpdateOrderAsync(originalClientOrderId, request, cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine($"Update order: clientOrderId={response.ClientOrderId}, status={response.Status}");
    }

    private static async Task CancelOrderAsync(SampleContext context, CancellationToken cancellationToken)
    {
        var originalClientOrderId = ParseRequiredGuid(
            "BCS_SAMPLE_ORIGINAL_CLIENT_ORDER_ID",
            "BCS_SAMPLE_CANCEL_ORIGINAL_CLIENT_ORDER_ID");

        var clientOrderId = ParseGuidWithDefault(
            Guid.NewGuid,
            "BCS_SAMPLE_CLIENT_ORDER_ID",
            "BCS_SAMPLE_CANCEL_CLIENT_ORDER_ID");

        var response = await context.Client.Orders
            .CancelOrderAsync(originalClientOrderId, clientOrderId, cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine($"Cancel order: clientOrderId={response.ClientOrderId}, status={response.Status}");
    }

    private static async Task StreamLastCandlesAsync(SampleContext context, CancellationToken cancellationToken)
    {
        var timeFrame = ParseEnumOrDefault("BCS_SAMPLE_CANDLE_TIMEFRAME", BcsCandleTimeFrames.Minute1);
        var subscribe = new BcsMarketDataSubscribeRequest
        {
            SubscribeType = BcsMarketDataSubscribeType.Subscribe,
            DataType = BcsMarketDataType.Candles,
            TimeFrame = ToMarketDataTimeFrameValue(timeFrame),
            Instruments = [CreateMarketDataInstrument(context)],
        };

        using var socket = await context.Client.WebSockets
            .ConnectAsync(BcsWebSocketEndpoints.MarketData, cancellationToken)
            .ConfigureAwait(false);

        await socket.SendJsonMessageAsync(subscribe, cancellationToken).ConfigureAwait(false);
        Console.WriteLine(
            $"Subscribed to candles for {context.ClassCode}/{context.Ticker} {subscribe.TimeFrame}.");

        using var timeout = CreateStreamTimeout(context);
        try
        {
            for (var received = 0; received < context.StreamMessageLimit;)
            {
                var message = await socket.ReceiveJsonMessageAsync<BcsLastCandleMessage>(timeout.Token)
                    .ConfigureAwait(false);
                if (message is null)
                {
                    Console.WriteLine("  Stream closed by server.");
                    break;
                }

                if (PrintMarketDataErrors(message.Errors))
                {
                    break;
                }

                if (string.Equals(message.ResponseType, "CandleStickSuccess", StringComparison.Ordinal))
                {
                    Console.WriteLine(
                        $"  Subscription confirmed for {message.Ticker}/{message.ClassCode} {message.TimeFrame}.");
                    continue;
                }

                received++;
                Console.WriteLine(
                    $"  {message.DateTime:O} {message.Ticker}/{message.ClassCode}: " +
                    $"O={message.Open} H={message.High} L={message.Low} C={message.Close} V={message.Volume}");
            }

            await socket.SendJsonMessageAsync(
                    subscribe with { SubscribeType = BcsMarketDataSubscribeType.Unsubscribe },
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("  Timed out waiting for candle messages.");
        }
    }

    private static async Task StreamOrderBookAsync(SampleContext context, CancellationToken cancellationToken)
    {
        var subscribe = new BcsOrderBookSubscribeRequest
        {
            SubscribeType = BcsMarketDataSubscribeType.Subscribe,
            DataType = BcsMarketDataType.OrderBook,
            Depth = ParseIntOrDefault("BCS_SAMPLE_ORDERBOOK_DEPTH", 20, minValue: 1),
            Instruments = [CreateMarketDataInstrument(context)],
        };

        using var socket = await context.Client.WebSockets
            .ConnectAsync(BcsWebSocketEndpoints.MarketData, cancellationToken)
            .ConfigureAwait(false);

        await socket.SendJsonMessageAsync(subscribe, cancellationToken).ConfigureAwait(false);
        Console.WriteLine(
            $"Subscribed to order book for {context.ClassCode}/{context.Ticker}, depth={subscribe.Depth}.");

        using var timeout = CreateStreamTimeout(context);
        try
        {
            for (var received = 0; received < context.StreamMessageLimit;)
            {
                var message = await socket.ReceiveJsonMessageAsync<BcsOrderBookMessage>(timeout.Token)
                    .ConfigureAwait(false);
                if (message is null)
                {
                    Console.WriteLine("  Stream closed by server.");
                    break;
                }

                if (PrintMarketDataErrors(message.Errors))
                {
                    break;
                }

                if (string.Equals(message.ResponseType, "OrderBookSuccess", StringComparison.Ordinal))
                {
                    Console.WriteLine(
                        $"  Subscription confirmed for {message.Ticker}/{message.ClassCode}, depth={message.Depth}.");
                    continue;
                }

                received++;
                var bestBid = message.Bids.Count > 0 ? message.Bids[0] : null;
                var bestAsk = message.Asks.Count > 0 ? message.Asks[0] : null;
                Console.WriteLine(
                    $"  {message.DateTime:O} {message.Ticker}/{message.ClassCode}: " +
                    $"bestBid={bestBid?.Price}x{bestBid?.Quantity} " +
                    $"bestAsk={bestAsk?.Price}x{bestAsk?.Quantity} " +
                    $"bidVol={message.BidVolume} askVol={message.AskVolume}");
            }

            await socket.SendJsonMessageAsync(
                    subscribe with { SubscribeType = BcsMarketDataSubscribeType.Unsubscribe },
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("  Timed out waiting for order-book messages.");
        }
    }

    private static async Task StreamQuotesAsync(SampleContext context, CancellationToken cancellationToken)
    {
        var subscribe = new BcsQuotesSubscribeRequest
        {
            SubscribeType = BcsMarketDataSubscribeType.Subscribe,
            DataType = BcsMarketDataType.Quotes,
            Instruments = [CreateMarketDataInstrument(context)],
        };

        using var socket = await context.Client.WebSockets
            .ConnectAsync(BcsWebSocketEndpoints.MarketData, cancellationToken)
            .ConfigureAwait(false);

        await socket.SendJsonMessageAsync(subscribe, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"Subscribed to quotes for {context.ClassCode}/{context.Ticker}.");

        using var timeout = CreateStreamTimeout(context);
        try
        {
            for (var received = 0; received < context.StreamMessageLimit; received++)
            {
                var message = await socket.ReceiveJsonMessageAsync<BcsQuotesMessage>(timeout.Token)
                    .ConfigureAwait(false);
                if (message is null)
                {
                    Console.WriteLine("  Stream closed by server.");
                    break;
                }

                if (PrintMarketDataErrors(message.Errors))
                {
                    break;
                }

                Console.WriteLine(
                    $"  {message.DateTime:O} {message.Ticker}/{message.ClassCode}: " +
                    $"last={message.Last} bid={message.Bid} offer={message.Offer} " +
                    $"change={message.Change} ({message.ChangeRate}%) status={message.SecurityTradingStatus}");
            }

            await socket.SendJsonMessageAsync(
                    subscribe with { SubscribeType = BcsMarketDataSubscribeType.Unsubscribe },
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("  Timed out waiting for quote messages.");
        }
    }

    private static async Task StreamLastTradesAsync(SampleContext context, CancellationToken cancellationToken)
    {
        var subscribe = new BcsLastTradesSubscribeRequest
        {
            SubscribeType = BcsMarketDataSubscribeType.Subscribe,
            DataType = BcsMarketDataType.AnonymousTrades,
            Instruments = [CreateMarketDataInstrument(context)],
        };

        using var socket = await context.Client.WebSockets
            .ConnectAsync(BcsWebSocketEndpoints.MarketData, cancellationToken)
            .ConfigureAwait(false);

        await socket.SendJsonMessageAsync(subscribe, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"Subscribed to anonymous trades for {context.ClassCode}/{context.Ticker}.");

        using var timeout = CreateStreamTimeout(context);
        try
        {
            for (var received = 0; received < context.StreamMessageLimit;)
            {
                var message = await socket.ReceiveJsonMessageAsync<BcsLastTradeMessage>(timeout.Token)
                    .ConfigureAwait(false);
                if (message is null)
                {
                    Console.WriteLine("  Stream closed by server.");
                    break;
                }

                if (PrintMarketDataErrors(message.Errors))
                {
                    break;
                }

                if (string.Equals(message.ResponseType, "LastTradesSuccess", StringComparison.Ordinal))
                {
                    Console.WriteLine($"  Subscription confirmed for {message.Ticker}/{message.ClassCode}.");
                    continue;
                }

                received++;
                Console.WriteLine(
                    $"  {message.DateTime:O} {message.Ticker}/{message.ClassCode}: " +
                    $"{message.Side} price={message.Price} qty={message.Quantity} volume={message.Volume}");
            }

            await socket.SendJsonMessageAsync(
                    subscribe with { SubscribeType = BcsMarketDataSubscribeType.Unsubscribe },
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("  Timed out waiting for trade messages.");
        }
    }

    private static async Task StreamLimitsAsync(SampleContext context, CancellationToken cancellationToken)
    {
        using var socket = await context.Client.WebSockets
            .ConnectAsync(BcsWebSocketEndpoints.Limits, cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine("Connected to limits stream.");

        using var timeout = CreateStreamTimeout(context);
        try
        {
            var received = 0;
            await foreach (var limits in socket.ReadAllAsync<BcsLimitsResponse>(timeout.Token).ConfigureAwait(false))
            {
                PrintLimits(limits);
                if (++received == context.StreamMessageLimit)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("  Timed out waiting for limits updates.");
        }
    }

    private static async Task StreamPortfolioAsync(SampleContext context, CancellationToken cancellationToken)
    {
        using var socket = await context.Client.WebSockets
            .ConnectAsync(BcsWebSocketEndpoints.Portfolio, cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine("Connected to portfolio stream.");

        using var timeout = CreateStreamTimeout(context);
        try
        {
            for (var received = 0; received < context.StreamMessageLimit; received++)
            {
                var positions = await socket.ReceiveJsonMessageAsync<BcsPortfolioItem[]>(timeout.Token)
                    .ConfigureAwait(false);
                if (positions is null)
                {
                    Console.WriteLine("  Stream closed by server.");
                    break;
                }

                Console.WriteLine($"  Update: {positions.Length} positions");
                foreach (var position in positions.Take(10))
                {
                    PrintPortfolioItem(position, prefix: "    ");
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("  Timed out waiting for portfolio updates.");
        }
    }

    private static Task StreamOrderExecutionsAsync(SampleContext context, CancellationToken cancellationToken) =>
        StreamOrderStatusAsync(
            context,
            BcsWebSocketEndpoints.OrderExecutions,
            "order execution",
            cancellationToken);

    private static Task StreamOrderTransactionsAsync(SampleContext context, CancellationToken cancellationToken) =>
        StreamOrderStatusAsync(
            context,
            BcsWebSocketEndpoints.OrderTransactions,
            "order transaction",
            cancellationToken);

    private static async Task StreamOrderStatusAsync(
        SampleContext context,
        Uri endpoint,
        string label,
        CancellationToken cancellationToken)
    {
        using var socket = await context.Client.WebSockets
            .ConnectAsync(endpoint, cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine($"Connected to {label} stream.");

        using var timeout = CreateStreamTimeout(context);
        try
        {
            for (var received = 0; received < context.StreamMessageLimit; received++)
            {
                var update = await socket.ReceiveJsonMessageAsync<BcsOrderStatusResponse>(timeout.Token)
                    .ConfigureAwait(false);
                if (update is null)
                {
                    Console.WriteLine("  Stream closed by server.");
                    break;
                }

                PrintOrderStatus(update);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"  Timed out waiting for {label} updates.");
        }
    }

    private static async Task StreamMarginalIndicatorsAsync(SampleContext context, CancellationToken cancellationToken)
    {
        using var socket = await context.Client.WebSockets
            .ConnectAsync(BcsWebSocketEndpoints.MarginalIndicators, cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine("Connected to marginal indicators stream.");

        using var timeout = CreateStreamTimeout(context);
        try
        {
            for (var received = 0; received < context.StreamMessageLimit; received++)
            {
                var snapshot = await socket.ReceiveJsonMessageAsync<BcsMarginalIndicators>(timeout.Token)
                    .ConfigureAwait(false);
                if (snapshot is null)
                {
                    Console.WriteLine("  Stream closed by server.");
                    break;
                }

                var value = snapshot.PortfolioCurrentValue;
                var stability = snapshot.PortfolioStability;
                Console.WriteLine(
                    $"  Account {snapshot.AgreementData?.Number} ({snapshot.AgreementData?.DisplayName}): " +
                    $"valueRub={value?.CurrentValueRub} daily={value?.DailyPortfolioChangePerc}% " +
                    $"marginOn={stability?.IsMarginOn} status={stability?.StabilityStatusName} " +
                    $"securities={snapshot.MarginSecurities?.MarginBySecurity.Count ?? 0} " +
                    $"futures={snapshot.FuturesParameters?.FuturesPortfolio.Count ?? 0}");
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("  Timed out waiting for marginal-indicators updates.");
        }
    }

    private static BcsMarketDataInstrument CreateMarketDataInstrument(SampleContext context) =>
        new()
        {
            ClassCode = context.ClassCode,
            Ticker = context.Ticker,
        };

    private static CancellationTokenSource CreateStreamTimeout(SampleContext context) =>
        new(context.StreamTimeout);

    private static bool PrintMarketDataErrors(IReadOnlyList<BcsMarketDataError> errors)
    {
        if (errors.Count == 0)
        {
            return false;
        }

        foreach (var error in errors)
        {
            Console.WriteLine($"  Error [{error.Code}]: {error.Message}");
        }

        return true;
    }

    private static BcsAuthClientIds GetConfiguredClientId(bool requiresWriteClient)
    {
        var rawClientId = GetOptionalString("BCS_SAMPLE_CLIENT_ID");
        var clientId = rawClientId?.Trim().ToLowerInvariant() switch
        {
            null => requiresWriteClient
                ? BcsAuthClientIds.TradeApiWrite
                : BcsAuthClientIds.TradeApiRead,
            "read" or "trade-api-read" or "tradeapiread" => BcsAuthClientIds.TradeApiRead,
            "write" or "trade-api-write" or "tradeapiwrite" => BcsAuthClientIds.TradeApiWrite,
            _ => throw new InvalidOperationException(
                "BCS_SAMPLE_CLIENT_ID must be read, write, trade-api-read, or trade-api-write."),
        };

        if (requiresWriteClient && clientId != BcsAuthClientIds.TradeApiWrite)
        {
            throw new InvalidOperationException(
                "This sample command requires BCS_SAMPLE_CLIENT_ID=write or an unset BCS_SAMPLE_CLIENT_ID.");
        }

        return clientId;
    }

    private static string GetRequiredString(params string[] variableNames)
    {
        var value = GetOptionalString(variableNames);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Set one of these environment variables: {string.Join(", ", variableNames)}.");
        }

        return value;
    }

    private static string? GetOptionalString(params string[] variableNames)
    {
        foreach (var variableName in variableNames)
        {
            var rawValue = Environment.GetEnvironmentVariable(variableName);
            if (!string.IsNullOrWhiteSpace(rawValue))
            {
                return rawValue.Trim();
            }
        }

        return null;
    }

    private static string GetStringOrDefault(string variableName, string defaultValue) =>
        GetOptionalString(variableName) ?? defaultValue;

    private static string[] SplitStringList(string variableName, string[] defaultValues)
    {
        var rawValue = GetOptionalString(variableName);
        if (rawValue is null)
        {
            return defaultValues;
        }

        var values = rawValue.Split(
            [',', ';'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (values.Length == 0)
        {
            throw new InvalidOperationException($"{variableName} must contain at least one value.");
        }

        return values;
    }

    private static int ParseIntOrDefault(string variableName, int defaultValue, int minValue)
    {
        var rawValue = GetOptionalString(variableName);
        if (rawValue is null)
        {
            return defaultValue;
        }

        if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            || value < minValue)
        {
            throw new InvalidOperationException(
                $"{variableName} must be an integer greater than or equal to {minValue}.");
        }

        return value;
    }

    private static long ParseRequiredPositiveInt64(params string[] variableNames)
    {
        var rawValue = GetRequiredString(variableNames);
        if (!long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            || value <= 0)
        {
            throw new InvalidOperationException(
                $"{string.Join(" or ", variableNames)} must be a positive integer.");
        }

        return value;
    }

    private static decimal? ParseOptionalPositiveDecimal(params string[] variableNames)
    {
        var rawValue = GetOptionalString(variableNames);
        if (rawValue is null)
        {
            return null;
        }

        if (!decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            || value <= 0)
        {
            throw new InvalidOperationException(
                $"{string.Join(" or ", variableNames)} must be a positive decimal number with '.' as decimal separator.");
        }

        return value;
    }

    private static Guid ParseRequiredGuid(params string[] variableNames)
    {
        var rawValue = GetRequiredString(variableNames);
        if (!Guid.TryParse(rawValue, out var value) || value == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"{string.Join(" or ", variableNames)} must be a non-empty UUID.");
        }

        return value;
    }

    private static Guid ParseGuidWithDefault(Func<Guid> defaultFactory, params string[] variableNames)
    {
        var rawValue = GetOptionalString(variableNames);
        if (rawValue is null)
        {
            return defaultFactory();
        }

        if (!Guid.TryParse(rawValue, out var value) || value == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"{string.Join(" or ", variableNames)} must be a non-empty UUID.");
        }

        return value;
    }

    private static DateTimeOffset ParseDateTimeOffsetOrDefault(string variableName, DateTimeOffset defaultValue)
    {
        var rawValue = GetOptionalString(variableName);
        if (rawValue is null)
        {
            return defaultValue;
        }

        if (!DateTimeOffset.TryParse(
                rawValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var value))
        {
            throw new InvalidOperationException($"{variableName} must be an ISO-8601 date/time value.");
        }

        return value;
    }

    private static TEnum ParseEnumOrDefault<TEnum>(string variableName, TEnum defaultValue)
        where TEnum : struct, Enum
    {
        return ParseEnumOrDefault(defaultValue, variableName);
    }

    private static TEnum ParseEnumOrDefault<TEnum>(TEnum defaultValue, params string[] variableNames)
        where TEnum : struct, Enum
    {
        var rawValue = GetOptionalString(variableNames);
        if (rawValue is null)
        {
            return defaultValue;
        }

        if (Enum.TryParse<TEnum>(rawValue, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        var names = string.Join(", ", Enum.GetNames<TEnum>());
        throw new InvalidOperationException(
            $"{string.Join(" or ", variableNames)} must be one of: {names}.");
    }

    private static string FormatTime(TimeOnly? value) =>
        value is null
            ? "(null)"
            : value.Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    private static string ToMarketDataTimeFrameValue(BcsCandleTimeFrames timeFrame) =>
        timeFrame switch
        {
            BcsCandleTimeFrames.Minute1 => "M1",
            BcsCandleTimeFrames.Minute5 => "M5",
            BcsCandleTimeFrames.Minute15 => "M15",
            BcsCandleTimeFrames.Minute30 => "M30",
            BcsCandleTimeFrames.Hour1 => "H1",
            BcsCandleTimeFrames.Hour4 => "H4",
            BcsCandleTimeFrames.Day => "D",
            BcsCandleTimeFrames.Week => "W",
            BcsCandleTimeFrames.Month => "MN",
            _ => throw new ArgumentOutOfRangeException(nameof(timeFrame), timeFrame, "Unsupported candle time frame."),
        };

    private sealed record SampleCommand(
        string Name,
        string Description,
        Func<SampleContext, CancellationToken, Task> ExecuteAsync,
        bool RequiresWriteClient = false);

    private sealed record SampleContext(
        BcsInvestApiClient Client,
        string ClassCode,
        string Ticker,
        string[] Isins,
        string[] Tickers,
        int Page,
        int Size,
        int StreamMessageLimit,
        TimeSpan StreamTimeout);
}

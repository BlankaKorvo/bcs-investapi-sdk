namespace Bcs.InvestApi.Services;

using Bcs.InvestApi;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Tokens;
using Bcs.InvestApi.WebSockets;

internal sealed class BcsInvestApiClientServices
{
    private BcsInvestApiClientServices(
        BcsTokenManager tokens,
        BcsLimitsService limits,
        BcsPortfolioService portfolio,
        BcsTradingScheduleService tradingSchedule,
        BcsInstrumentsService instruments,
        BcsMarketDataService marketData,
        BcsOrdersService orders,
        BcsTradesService trades,
        BcsMarginalIndicatorsService marginalIndicators,
        BcsWebSocketConnector webSockets)
    {
        Tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        Limits = limits ?? throw new ArgumentNullException(nameof(limits));
        Portfolio = portfolio ?? throw new ArgumentNullException(nameof(portfolio));
        TradingSchedule = tradingSchedule ?? throw new ArgumentNullException(nameof(tradingSchedule));
        Instruments = instruments ?? throw new ArgumentNullException(nameof(instruments));
        MarketData = marketData ?? throw new ArgumentNullException(nameof(marketData));
        Orders = orders ?? throw new ArgumentNullException(nameof(orders));
        Trades = trades ?? throw new ArgumentNullException(nameof(trades));
        MarginalIndicators = marginalIndicators ?? throw new ArgumentNullException(nameof(marginalIndicators));
        WebSockets = webSockets ?? throw new ArgumentNullException(nameof(webSockets));
    }

    internal BcsTokenManager Tokens { get; }

    internal BcsLimitsService Limits { get; }

    internal BcsPortfolioService Portfolio { get; }

    internal BcsTradingScheduleService TradingSchedule { get; }

    internal BcsInstrumentsService Instruments { get; }

    internal BcsMarketDataService MarketData { get; }

    internal BcsOrdersService Orders { get; }

    internal BcsTradesService Trades { get; }

    internal BcsMarginalIndicatorsService MarginalIndicators { get; }

    internal BcsWebSocketConnector WebSockets { get; }

    internal static BcsInvestApiClientServices Create(
        BcsInvestApiSettings settings,
        HttpClient httpClient,
        TimeProvider? timeProvider,
        IBcsHttpSender? requestSender = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClient);

        var sender = requestSender ?? new BcsHttpRequestSender();
        var auth = BcsAuthService.ForSharedClient(httpClient, settings, sender);
        var tokens = new BcsTokenManager(auth, settings, timeProvider);

        return new BcsInvestApiClientServices(
            tokens,
            new BcsLimitsService(settings, httpClient, tokens, sender),
            new BcsPortfolioService(settings, httpClient, tokens, sender),
            new BcsTradingScheduleService(settings, httpClient, tokens, sender),
            new BcsInstrumentsService(settings, httpClient, tokens, sender),
            new BcsMarketDataService(settings, httpClient, tokens, sender),
            new BcsOrdersService(settings, httpClient, tokens, sender),
            new BcsTradesService(settings, httpClient, tokens, sender),
            new BcsMarginalIndicatorsService(settings, httpClient, tokens, sender),
            new BcsWebSocketConnector(
                tokens,
                settings.WebSocketKeepAliveInterval,
                settings.WebSocketKeepAliveTimeout));
    }

    internal BcsInvestApiClient CreateClient(
        bool ownsTokenManager = false,
        IDisposable? ownedTransport = null) =>
        new(
            Tokens,
            Limits,
            Portfolio,
            TradingSchedule,
            Instruments,
            MarketData,
            Orders,
            Trades,
            MarginalIndicators,
            WebSockets,
            ownsTokenManager,
            ownedTransport);
}

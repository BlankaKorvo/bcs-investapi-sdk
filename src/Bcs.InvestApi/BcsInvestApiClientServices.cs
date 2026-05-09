namespace Bcs.InvestApi;

using Bcs.InvestApi.Auth;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Instruments;
using Bcs.InvestApi.Limits;
using Bcs.InvestApi.MarketData;
using Bcs.InvestApi.Portfolio;
using Bcs.InvestApi.Time;
using Bcs.InvestApi.Tokens;
using Bcs.InvestApi.TradingSchedule;

internal sealed class BcsInvestApiClientServices
{
    private BcsInvestApiClientServices(
        BcsTokenManager tokens,
        BcsLimitsService limits,
        BcsPortfolioService portfolio,
        BcsTradingScheduleService tradingSchedule,
        BcsInstrumentsService instruments,
        BcsMarketDataService marketData)
    {
        Tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        Limits = limits ?? throw new ArgumentNullException(nameof(limits));
        Portfolio = portfolio ?? throw new ArgumentNullException(nameof(portfolio));
        TradingSchedule = tradingSchedule ?? throw new ArgumentNullException(nameof(tradingSchedule));
        Instruments = instruments ?? throw new ArgumentNullException(nameof(instruments));
        MarketData = marketData ?? throw new ArgumentNullException(nameof(marketData));
    }

    internal BcsTokenManager Tokens { get; }

    internal BcsLimitsService Limits { get; }

    internal BcsPortfolioService Portfolio { get; }

    internal BcsTradingScheduleService TradingSchedule { get; }

    internal BcsInstrumentsService Instruments { get; }

    internal BcsMarketDataService MarketData { get; }

    internal static BcsInvestApiClientServices Create(
        BcsInvestApiSettings settings,
        HttpClient httpClient,
        IBcsClock? clock,
        IBcsHttpSender? requestSender = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClient);

        var sender = requestSender ?? new BcsHttpRequestSender();
        var auth = new BcsAuthService(httpClient, settings, sender);
        var tokens = new BcsTokenManager(auth, settings, clock);

        return new BcsInvestApiClientServices(
            tokens,
            new BcsLimitsService(settings, httpClient, tokens, sender),
            new BcsPortfolioService(settings, httpClient, tokens, sender),
            new BcsTradingScheduleService(settings, httpClient, tokens, sender),
            new BcsInstrumentsService(settings, httpClient, tokens, sender),
            new BcsMarketDataService(settings, httpClient, tokens, sender));
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
            ownsTokenManager,
            ownedTransport);
}

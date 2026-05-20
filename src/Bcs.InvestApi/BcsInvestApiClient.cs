namespace Bcs.InvestApi;

using Bcs.InvestApi.Services;
using Bcs.InvestApi.Tokens;
using Bcs.InvestApi.WebSockets;

/// <summary>
/// Thin facade over BCS Trade API service clients.
/// </summary>
/// <remarks>
/// Endpoint methods throw <see cref="Contracts.Exceptions.BcsApiException"/> on non-success responses and
/// never retry. On a <c>401 invalid_token</c> the cached access token is dropped before the exception
/// propagates, so the next call obtains a fresh token automatically; retrying after such a 401 is the
/// caller's responsibility.
/// </remarks>
public sealed class BcsInvestApiClient : IDisposable, IAsyncDisposable
{
    private readonly BcsTokenManager _tokens;
    private readonly BcsLimitsService _limits;
    private readonly BcsPortfolioService _portfolio;
    private readonly BcsTradingScheduleService _tradingSchedule;
    private readonly BcsInstrumentsService _instruments;
    private readonly BcsMarketDataService _marketData;
    private readonly BcsOrdersService _orders;
    private readonly BcsTradesService _trades;
    private readonly BcsMarginalIndicatorsService _marginalIndicators;
    private readonly BcsWebSocketConnector _webSockets;
    private readonly bool _ownsTokenManager;
    private readonly IDisposable? _ownedTransport;
    private bool _disposed;

    internal BcsInvestApiClient(
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
        : this(
            tokens,
            limits,
            portfolio,
            tradingSchedule,
            instruments,
            marketData,
            orders,
            trades,
            marginalIndicators,
            webSockets,
            ownsTokenManager: false,
            ownedTransport: null)
    {
    }

    internal BcsInvestApiClient(
        BcsTokenManager tokens,
        BcsLimitsService limits,
        BcsPortfolioService portfolio,
        BcsTradingScheduleService tradingSchedule,
        BcsInstrumentsService instruments,
        BcsMarketDataService marketData,
        BcsOrdersService orders,
        BcsTradesService trades,
        BcsMarginalIndicatorsService marginalIndicators,
        BcsWebSocketConnector webSockets,
        bool ownsTokenManager,
        IDisposable? ownedTransport)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _limits = limits ?? throw new ArgumentNullException(nameof(limits));
        _portfolio = portfolio ?? throw new ArgumentNullException(nameof(portfolio));
        _tradingSchedule = tradingSchedule ?? throw new ArgumentNullException(nameof(tradingSchedule));
        _instruments = instruments ?? throw new ArgumentNullException(nameof(instruments));
        _marketData = marketData ?? throw new ArgumentNullException(nameof(marketData));
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _trades = trades ?? throw new ArgumentNullException(nameof(trades));
        _marginalIndicators = marginalIndicators ?? throw new ArgumentNullException(nameof(marginalIndicators));
        _webSockets = webSockets ?? throw new ArgumentNullException(nameof(webSockets));
        _ownsTokenManager = ownsTokenManager;
        _ownedTransport = ownedTransport;
    }

    /// <summary>Limits endpoint.</summary>
    public IBcsLimitsApi Limits => NotDisposed(_limits);

    /// <summary>Portfolio endpoint.</summary>
    public IBcsPortfolioApi Portfolio => NotDisposed(_portfolio);

    /// <summary>Trading schedule endpoints.</summary>
    public IBcsTradingScheduleApi TradingSchedule => NotDisposed(_tradingSchedule);

    /// <summary>Instruments endpoints.</summary>
    public IBcsInstrumentsApi Instruments => NotDisposed(_instruments);

    /// <summary>Market data endpoints.</summary>
    public IBcsMarketDataApi MarketData => NotDisposed(_marketData);

    /// <summary>Orders endpoints.</summary>
    public IBcsOrdersApi Orders => NotDisposed(_orders);

    /// <summary>Trades endpoints.</summary>
    public IBcsTradesApi Trades => NotDisposed(_trades);

    /// <summary>Marginal indicators endpoint.</summary>
    public IBcsMarginalIndicatorsApi MarginalIndicators => NotDisposed(_marginalIndicators);

    /// <summary>
    /// WebSocket connector for BCS streaming endpoints (see <see cref="BcsWebSocketEndpoints"/> for known URLs).
    /// Keep-alive ping/pong is configured from <see cref="BcsInvestApiSettings"/>; the caller owns disposal and
    /// the rest of the lifecycle (reconnect, backoff) and must keep receiving so the server's pings are answered.
    /// </summary>
    public BcsWebSocketConnector WebSockets => NotDisposed(_webSockets);

    private T NotDisposed<T>(T api)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return api;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_ownsTokenManager)
        {
            _tokens.Dispose();
        }

        _ownedTransport?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_ownsTokenManager)
        {
            await _tokens.DisposeAsync().ConfigureAwait(false);
        }

        _ownedTransport?.Dispose();
    }
}

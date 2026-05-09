namespace Bcs.InvestApi;

using Bcs.InvestApi.Auth;
using Bcs.InvestApi.Instruments;
using Bcs.InvestApi.Limits;
using Bcs.InvestApi.MarketData;
using Bcs.InvestApi.Portfolio;
using Bcs.InvestApi.Tokens;
using Bcs.InvestApi.TradingSchedule;

/// <summary>
/// Thin facade over BCS Trade API service clients.
/// </summary>
public sealed class BcsInvestApiClient : IDisposable, IAsyncDisposable
{
    private readonly BcsTokenManager _tokens;
    private readonly BcsLimitsService _limits;
    private readonly BcsPortfolioService _portfolio;
    private readonly BcsTradingScheduleService _tradingSchedule;
    private readonly BcsInstrumentsService _instruments;
    private readonly BcsMarketDataService _marketData;
    private readonly bool _ownsTokenManager;
    private readonly IDisposable? _ownedTransport;
    private bool _disposed;

    internal BcsInvestApiClient(
        BcsAuthService auth,
        BcsTokenManager tokens,
        BcsLimitsService limits,
        BcsPortfolioService portfolio,
        BcsTradingScheduleService tradingSchedule,
        BcsInstrumentsService instruments,
        BcsMarketDataService marketData)
        : this(
            auth,
            tokens,
            limits,
            portfolio,
            tradingSchedule,
            instruments,
            marketData,
            ownsTokenManager: false,
            ownedTransport: null)
    {
    }

    internal BcsInvestApiClient(
        BcsAuthService auth,
        BcsTokenManager tokens,
        BcsLimitsService limits,
        BcsPortfolioService portfolio,
        BcsTradingScheduleService tradingSchedule,
        BcsInstrumentsService instruments,
        BcsMarketDataService marketData,
        bool ownsTokenManager,
        IDisposable? ownedTransport)
    {
        Auth = auth ?? throw new ArgumentNullException(nameof(auth));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _limits = limits ?? throw new ArgumentNullException(nameof(limits));
        _portfolio = portfolio ?? throw new ArgumentNullException(nameof(portfolio));
        _tradingSchedule = tradingSchedule ?? throw new ArgumentNullException(nameof(tradingSchedule));
        _instruments = instruments ?? throw new ArgumentNullException(nameof(instruments));
        _marketData = marketData ?? throw new ArgumentNullException(nameof(marketData));
        _ownsTokenManager = ownsTokenManager;
        _ownedTransport = ownedTransport;
    }

    internal BcsAuthService Auth { get; }

    public Task<BcsLimitsResponse> GetLimitsAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _limits.GetLimitsAsync(cancellationToken);
    }

    public Task<IReadOnlyList<BcsPortfolioPosition>> GetPortfolioAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _portfolio.GetPortfolioAsync(cancellationToken);
    }

    public Task<BcsDailyTradingScheduleResponse> GetDailyTradingScheduleAsync(
        string classCode,
        string ticker,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _tradingSchedule.GetDailyTradingScheduleAsync(classCode, ticker, cancellationToken);
    }

    public Task<BcsCandlesResponse> GetCandlesAsync(
        string classCode,
        string ticker,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        string timeFrame,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _marketData.GetCandlesAsync(
            classCode,
            ticker,
            startDate,
            endDate,
            timeFrame,
            cancellationToken);
    }

    public Task<IReadOnlyList<BcsInstrument>> GetInstrumentsByIsinsAsync(
        IEnumerable<string> isins,
        int size = 50,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instruments.GetInstrumentsByIsinsAsync(isins, size, cancellationToken);
    }

    public Task<IReadOnlyList<BcsInstrument>> GetInstrumentsByIsinsPageAsync(
        IEnumerable<string> isins,
        int page,
        int size = 50,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instruments.GetInstrumentsByIsinsPageAsync(isins, page, size, cancellationToken);
    }

    public Task<IReadOnlyList<BcsInstrument>> GetInstrumentsByTickersAsync(
        IEnumerable<string> tickers,
        int size = 50,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instruments.GetInstrumentsByTickersAsync(tickers, size, cancellationToken);
    }

    public Task<IReadOnlyList<BcsInstrument>> GetInstrumentsByTickersPageAsync(
        IEnumerable<string> tickers,
        int page,
        int size = 50,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instruments.GetInstrumentsByTickersPageAsync(tickers, page, size, cancellationToken);
    }

    public Task<IReadOnlyList<BcsInstrument>> GetInstrumentsByTypeAsync(
        string type,
        int size = 50,
        string? baseAssetTicker = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instruments.GetInstrumentsByTypeAsync(type, size, baseAssetTicker, cancellationToken);
    }

    public Task<IReadOnlyList<BcsInstrument>> GetInstrumentsByTypePageAsync(
        string type,
        int page,
        int size = 50,
        string? baseAssetTicker = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instruments.GetInstrumentsByTypePageAsync(type, page, size, baseAssetTicker, cancellationToken);
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

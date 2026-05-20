namespace Bcs.InvestApi;

using Bcs.InvestApi.Contracts.Enums;
using Bcs.InvestApi.Contracts.Instruments;

/// <summary>Instruments endpoints of the BCS Trade API.</summary>
public interface IBcsInstrumentsApi
{
    Task<IReadOnlyList<BcsInstrument>> GetInstrumentsByIsinsAsync(
        IEnumerable<string> isins,
        int page,
        int size,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BcsInstrument>> GetInstrumentsByTickersAsync(
        IEnumerable<string> tickers,
        int page,
        int size,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BcsInstrument>> GetInstrumentsByTypeAsync(
        BcsInstrumentTypes type,
        int page,
        int size,
        string? baseAssetTicker = null,
        CancellationToken cancellationToken = default);
}

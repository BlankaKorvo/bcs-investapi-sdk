namespace Bcs.InvestApi.Contracts.Instruments;

internal sealed record BcsInstrumentsByTickersRequest(IReadOnlyList<string> Tickers);

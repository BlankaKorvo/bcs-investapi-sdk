namespace Bcs.InvestApi.DTO;

internal sealed record BcsInstrumentsByTickersRequest(IReadOnlyList<string> Tickers);

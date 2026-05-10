namespace Bcs.InvestApi.Contracts.Instruments;

internal sealed record BcsInstrumentsByIsinsRequest(IReadOnlyList<string> Isins);

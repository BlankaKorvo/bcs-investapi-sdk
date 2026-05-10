namespace Bcs.InvestApi.DTO;

internal sealed record BcsInstrumentsByIsinsRequest(IReadOnlyList<string> Isins);

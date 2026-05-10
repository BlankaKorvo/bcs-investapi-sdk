namespace Bcs.InvestApi.DTO.Enums;

public enum BcsInstrumentTypes
{
    Currency,
    Stock,
    ForeignStock,
    Bonds,
    Notes,
    DepositaryReceipts,
    EuroBonds,
    MutualFunds,
    Etf,
    Futures,
    Options,
    Goods,
    Indices,
}

internal static class BcsInstrumentTypesExtensions
{
    internal static string ToApiValue(this BcsInstrumentTypes type) =>
        type switch
        {
            BcsInstrumentTypes.Currency => "CURRENCY",
            BcsInstrumentTypes.Stock => "STOCK",
            BcsInstrumentTypes.ForeignStock => "FOREIGN_STOCK",
            BcsInstrumentTypes.Bonds => "BONDS",
            BcsInstrumentTypes.Notes => "NOTES",
            BcsInstrumentTypes.DepositaryReceipts => "DEPOSITARY_RECEIPTS",
            BcsInstrumentTypes.EuroBonds => "EURO_BONDS",
            BcsInstrumentTypes.MutualFunds => "MUTUAL_FUNDS",
            BcsInstrumentTypes.Etf => "ETF",
            BcsInstrumentTypes.Futures => "FUTURES",
            BcsInstrumentTypes.Options => "OPTIONS",
            BcsInstrumentTypes.Goods => "GOODS",
            BcsInstrumentTypes.Indices => "INDICES",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported BCS instrument type."),
        };
}

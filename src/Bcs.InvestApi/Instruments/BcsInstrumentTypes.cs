namespace Bcs.InvestApi.Instruments;

public static class BcsInstrumentTypes
{
    public const string Currency = "CURRENCY";
    public const string Stock = "STOCK";
    public const string ForeignStock = "FOREIGN_STOCK";
    public const string Bonds = "BONDS";
    public const string Notes = "NOTES";
    public const string DepositaryReceipts = "DEPOSITARY_RECEIPTS";
    public const string EuroBonds = "EURO_BONDS";
    public const string MutualFunds = "MUTUAL_FUNDS";
    public const string Etf = "ETF";
    public const string Futures = "FUTURES";
    public const string Options = "OPTIONS";
    public const string Goods = "GOODS";
    public const string Indices = "INDICES";

    public static bool IsKnown(string? type) =>
        string.Equals(type, Currency, StringComparison.Ordinal) ||
        string.Equals(type, Stock, StringComparison.Ordinal) ||
        string.Equals(type, ForeignStock, StringComparison.Ordinal) ||
        string.Equals(type, Bonds, StringComparison.Ordinal) ||
        string.Equals(type, Notes, StringComparison.Ordinal) ||
        string.Equals(type, DepositaryReceipts, StringComparison.Ordinal) ||
        string.Equals(type, EuroBonds, StringComparison.Ordinal) ||
        string.Equals(type, MutualFunds, StringComparison.Ordinal) ||
        string.Equals(type, Etf, StringComparison.Ordinal) ||
        string.Equals(type, Futures, StringComparison.Ordinal) ||
        string.Equals(type, Options, StringComparison.Ordinal) ||
        string.Equals(type, Goods, StringComparison.Ordinal) ||
        string.Equals(type, Indices, StringComparison.Ordinal);
}

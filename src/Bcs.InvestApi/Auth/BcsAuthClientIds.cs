namespace Bcs.InvestApi.Auth;

public static class BcsAuthClientIds
{
    public const string TradeApiRead = "trade-api-read";
    public const string TradeApiWrite = "trade-api-write";

    public static bool IsKnown(string? clientId) =>
        string.Equals(clientId, TradeApiRead, StringComparison.Ordinal) ||
        string.Equals(clientId, TradeApiWrite, StringComparison.Ordinal);
}

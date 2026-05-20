namespace Bcs.InvestApi.Contracts.Enums;

public enum BcsTradeSort
{
    TradeDateTimeAsc,
    TradeDateTimeDesc,
    TickerAsc,
    TickerDesc,
    ClassCodeAsc,
    ClassCodeDesc,
    SideAsc,
    SideDesc,
}

internal static class BcsTradeSortExtensions
{
    internal static string ToApiValue(this BcsTradeSort sort) =>
        sort switch
        {
            BcsTradeSort.TradeDateTimeAsc => "tradeDateTime,asc",
            BcsTradeSort.TradeDateTimeDesc => "tradeDateTime,desc",
            BcsTradeSort.TickerAsc => "ticker,asc",
            BcsTradeSort.TickerDesc => "ticker,desc",
            BcsTradeSort.ClassCodeAsc => "classCode,asc",
            BcsTradeSort.ClassCodeDesc => "classCode,desc",
            BcsTradeSort.SideAsc => "side,asc",
            BcsTradeSort.SideDesc => "side,desc",
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Unsupported BCS trade sort."),
        };
}

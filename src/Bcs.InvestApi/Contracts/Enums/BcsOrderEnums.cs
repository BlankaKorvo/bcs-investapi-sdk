namespace Bcs.InvestApi.Contracts.Enums;

public enum BcsOrderSide
{
    Buy = 1,
    Sell = 2,
}

public enum BcsOrderStatus
{
    Canceled = 1,
    Executed = 2,
    Active = 3,
}

public enum BcsOrderType
{
    Market = 1,
    Limit = 2,
    Iceberg = 3,
    StopLimit = 4,
    TakeProfitWithLimitOrder = 5,
    StopLoss = 6,
    TakeProfitAndStopLoss = 7,
    LimitForThirtyDays = 10,
    TakeProfit = 11,
    TrailingStop = 12,
}

public enum BcsOrderSort
{
    OrderDateTimeAsc,
    OrderDateTimeDesc,
    UpdateDateTimeAsc,
    UpdateDateTimeDesc,
    TickerAsc,
    TickerDesc,
    ClassCodeAsc,
    ClassCodeDesc,
    OrderTypeAsc,
    OrderTypeDesc,
    SideAsc,
    SideDesc,
}

internal static class BcsOrderSideExtensions
{
    internal static int ToApiValue(this BcsOrderSide side) =>
        side switch
        {
            BcsOrderSide.Buy => 1,
            BcsOrderSide.Sell => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unsupported BCS order side."),
        };
}

internal static class BcsOrderStatusExtensions
{
    internal static int ToApiValue(this BcsOrderStatus status) =>
        status switch
        {
            BcsOrderStatus.Canceled => 1,
            BcsOrderStatus.Executed => 2,
            BcsOrderStatus.Active => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported BCS order status."),
        };
}

internal static class BcsOrderTypeExtensions
{
    internal static int ToApiValue(this BcsOrderType type) =>
        type switch
        {
            BcsOrderType.Market => 1,
            BcsOrderType.Limit => 2,
            BcsOrderType.Iceberg => 3,
            BcsOrderType.StopLimit => 4,
            BcsOrderType.TakeProfitWithLimitOrder => 5,
            BcsOrderType.StopLoss => 6,
            BcsOrderType.TakeProfitAndStopLoss => 7,
            BcsOrderType.LimitForThirtyDays => 10,
            BcsOrderType.TakeProfit => 11,
            BcsOrderType.TrailingStop => 12,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported BCS order type."),
        };
}

internal static class BcsOrderSortExtensions
{
    internal static string ToApiValue(this BcsOrderSort sort) =>
        sort switch
        {
            BcsOrderSort.OrderDateTimeAsc => "orderDateTime,asc",
            BcsOrderSort.OrderDateTimeDesc => "orderDateTime,desc",
            BcsOrderSort.UpdateDateTimeAsc => "updateDateTime,asc",
            BcsOrderSort.UpdateDateTimeDesc => "updateDateTime,desc",
            BcsOrderSort.TickerAsc => "ticker,asc",
            BcsOrderSort.TickerDesc => "ticker,desc",
            BcsOrderSort.ClassCodeAsc => "classCode,asc",
            BcsOrderSort.ClassCodeDesc => "classCode,desc",
            BcsOrderSort.OrderTypeAsc => "orderType,asc",
            BcsOrderSort.OrderTypeDesc => "orderType,desc",
            BcsOrderSort.SideAsc => "side,asc",
            BcsOrderSort.SideDesc => "side,desc",
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Unsupported BCS order sort."),
        };
}

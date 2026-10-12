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

/// <summary>
/// Order type of order creation (<c>orderType</c> of <c>POST /orders</c> and <c>/orders/sync</c>).
/// BCS numbers creation types differently from the order-list types (<see cref="BcsSearchOrderType"/>):
/// <c>3</c>–<c>5</c> are algorithmic GTD, iceberg and conditional orders there. Only the types this SDK can
/// send with every field they need are listed.
/// </summary>
public enum BcsCreateOrderType
{
    Market = 1,
    Limit = 2,
}

/// <summary>
/// Order type of the order list (<c>orderTypes</c> filter and <c>orderType</c> of
/// <c>POST /orders/search</c> records).
/// </summary>
public enum BcsSearchOrderType
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
}

/// <summary>
/// Kind of order identifier of the order status (<c>orderIdType</c> of <c>GET /orders</c>).
/// </summary>
public enum BcsOrderIdType
{
    /// <summary>The <c>clientOrderId</c> UUID the order was created with.</summary>
    ClientOrderId = 1,

    /// <summary>The exchange order identifier <c>YYMMDD-ClassCode-OrderNumber</c>.</summary>
    ExchangeOrderId = 2,
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

internal static class BcsCreateOrderTypeExtensions
{
    internal static int ToApiValue(this BcsCreateOrderType type) =>
        type switch
        {
            BcsCreateOrderType.Market => 1,
            BcsCreateOrderType.Limit => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported BCS order type for order creation."),
        };
}

internal static class BcsSearchOrderTypeExtensions
{
    internal static int ToApiValue(this BcsSearchOrderType type) =>
        type switch
        {
            BcsSearchOrderType.Market => 1,
            BcsSearchOrderType.Limit => 2,
            BcsSearchOrderType.Iceberg => 3,
            BcsSearchOrderType.StopLimit => 4,
            BcsSearchOrderType.TakeProfitWithLimitOrder => 5,
            BcsSearchOrderType.StopLoss => 6,
            BcsSearchOrderType.TakeProfitAndStopLoss => 7,
            BcsSearchOrderType.LimitForThirtyDays => 10,
            BcsSearchOrderType.TakeProfit => 11,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported BCS order type for the order list."),
        };
}

internal static class BcsOrderIdTypeExtensions
{
    internal static string ToApiValue(this BcsOrderIdType type) =>
        type switch
        {
            BcsOrderIdType.ClientOrderId => "1",
            BcsOrderIdType.ExchangeOrderId => "2",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported BCS order identifier type."),
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

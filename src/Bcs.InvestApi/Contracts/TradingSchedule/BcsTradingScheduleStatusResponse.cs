namespace Bcs.InvestApi.Contracts.TradingSchedule;

public sealed record BcsTradingScheduleStatusResponse
{
    public int? TradingSessionTypeId { get; init; }

    public string? TradingSessionType { get; init; }

    public string? TradingSessionStatus { get; init; }

    public DateTimeOffset? NextSessionDate { get; init; }
}

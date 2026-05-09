namespace Bcs.InvestApi.TradingSchedule;

public sealed record BcsDailyTradingScheduleResponse
{
    public bool IsWorkDay { get; init; }

    public IReadOnlyList<BcsDailyTradingScheduleEntry> DailySchedule { get; init; } =
        Array.Empty<BcsDailyTradingScheduleEntry>();
}

public sealed record BcsDailyTradingScheduleEntry
{
    public TimeOnly StartDate { get; init; }

    public TimeOnly EndDate { get; init; }

    public string? TradingSessionType { get; init; }

    public string? TradingSessionStatus { get; init; }
}

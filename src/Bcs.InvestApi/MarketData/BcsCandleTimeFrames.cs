namespace Bcs.InvestApi.MarketData;

public static class BcsCandleTimeFrames
{
    public const string Minute1 = "M1";
    public const string Minute5 = "M5";
    public const string Minute15 = "M15";
    public const string Minute30 = "M30";
    public const string Hour1 = "H1";
    public const string Hour4 = "H4";
    public const string Day = "D";
    public const string Week = "W";
    public const string Month = "MN";

    public static bool IsKnown(string? timeFrame) =>
        string.Equals(timeFrame, Minute1, StringComparison.Ordinal) ||
        string.Equals(timeFrame, Minute5, StringComparison.Ordinal) ||
        string.Equals(timeFrame, Minute15, StringComparison.Ordinal) ||
        string.Equals(timeFrame, Minute30, StringComparison.Ordinal) ||
        string.Equals(timeFrame, Hour1, StringComparison.Ordinal) ||
        string.Equals(timeFrame, Hour4, StringComparison.Ordinal) ||
        string.Equals(timeFrame, Day, StringComparison.Ordinal) ||
        string.Equals(timeFrame, Week, StringComparison.Ordinal) ||
        string.Equals(timeFrame, Month, StringComparison.Ordinal);

    internal static TimeSpan? GetFixedDuration(string timeFrame) =>
        timeFrame switch
        {
            Minute1 => TimeSpan.FromMinutes(1),
            Minute5 => TimeSpan.FromMinutes(5),
            Minute15 => TimeSpan.FromMinutes(15),
            Minute30 => TimeSpan.FromMinutes(30),
            Hour1 => TimeSpan.FromHours(1),
            Hour4 => TimeSpan.FromHours(4),
            Day => TimeSpan.FromDays(1),
            Week => TimeSpan.FromDays(7),
            _ => null,
        };
}

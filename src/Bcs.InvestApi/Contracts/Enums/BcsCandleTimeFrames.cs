namespace Bcs.InvestApi.Contracts.Enums;

public enum BcsCandleTimeFrames
{
    Minute1,
    Minute5,
    Minute15,
    Minute30,
    Hour1,
    Hour4,
    Day,
    Week,
    Month,
}

internal static class BcsCandleTimeFramesExtensions
{
    internal static string ToApiValue(this BcsCandleTimeFrames timeFrame) =>
        timeFrame switch
        {
            BcsCandleTimeFrames.Minute1 => "M1",
            BcsCandleTimeFrames.Minute5 => "M5",
            BcsCandleTimeFrames.Minute15 => "M15",
            BcsCandleTimeFrames.Minute30 => "M30",
            BcsCandleTimeFrames.Hour1 => "H1",
            BcsCandleTimeFrames.Hour4 => "H4",
            BcsCandleTimeFrames.Day => "D",
            BcsCandleTimeFrames.Week => "W",
            BcsCandleTimeFrames.Month => "MN",
            _ => throw new ArgumentOutOfRangeException(nameof(timeFrame), timeFrame, "Unsupported BCS candle time frame."),
        };
}

namespace Bcs.InvestApi.Time;

public sealed class BcsSystemClock : IBcsClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

namespace Bcs.InvestApi.Tests.Infrastructure;

using Bcs.InvestApi.Time;

internal sealed class FakeBcsClock : IBcsClock
{
    public FakeBcsClock(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; set; }
}

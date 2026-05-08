namespace Bcs.InvestApi.Time;

public interface IBcsClock
{
    DateTimeOffset UtcNow { get; }
}

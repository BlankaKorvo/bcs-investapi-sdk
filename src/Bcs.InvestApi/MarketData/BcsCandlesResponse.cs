namespace Bcs.InvestApi.MarketData;

public sealed record BcsCandlesResponse
{
    public string? Ticker { get; init; }

    public string? ClassCode { get; init; }

    public DateTimeOffset? StartDate { get; init; }

    public DateTimeOffset? EndDate { get; init; }

    public string? TimeFrame { get; init; }

    public IReadOnlyList<BcsCandleBar> Bars { get; init; } =
        Array.Empty<BcsCandleBar>();
}

public sealed record BcsCandleBar
{
    public DateTimeOffset? Time { get; init; }

    public decimal? Open { get; init; }

    public decimal? Close { get; init; }

    public decimal? High { get; init; }

    public decimal? Low { get; init; }

    public decimal? Volume { get; init; }
}

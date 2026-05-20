namespace Bcs.InvestApi.Contracts.Trades;

using Bcs.InvestApi.Contracts.Enums;

public sealed record BcsTradesSearchRequest
{
    public BcsOrderSide? Side { get; init; }

    public IReadOnlyList<long>? TradeNums { get; init; }

    public IReadOnlyList<string>? Tickers { get; init; }

    public IReadOnlyList<string>? ClassCodes { get; init; }

    public DateTimeOffset? StartDateTime { get; init; }

    public DateTimeOffset? EndDateTime { get; init; }
}

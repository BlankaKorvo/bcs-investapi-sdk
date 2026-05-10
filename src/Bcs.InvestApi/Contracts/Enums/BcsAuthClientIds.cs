namespace Bcs.InvestApi.Contracts.Enums;

public enum BcsAuthClientIds
{
    TradeApiRead,
    TradeApiWrite,
}

internal static class BcsAuthClientIdsExtensions
{
    internal static string ToApiValue(this BcsAuthClientIds clientId) =>
        clientId switch
        {
            BcsAuthClientIds.TradeApiRead => "trade-api-read",
            BcsAuthClientIds.TradeApiWrite => "trade-api-write",
            _ => throw new ArgumentOutOfRangeException(nameof(clientId), clientId, "Unsupported BCS auth client id."),
        };
}

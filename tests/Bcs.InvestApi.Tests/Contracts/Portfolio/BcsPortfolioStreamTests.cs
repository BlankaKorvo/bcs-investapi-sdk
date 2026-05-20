namespace Bcs.InvestApi.Tests.Contracts.Portfolio;

using System.Text.Json;
using Bcs.InvestApi.Contracts.Portfolio;
using Xunit;

public sealed class BcsPortfolioStreamTests
{
    // Mirrors the SDK's transport options (Bcs.InvestApi.Infrastructure.BcsJson).
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void PortfolioStreamMessage_Deserializes_AsArray()
    {
        // The portfolio WebSocket pushes the same array payload as the HTTP /portfolio endpoint.
        const string payload = """
        [
          {
            "type": "STOCK",
            "ticker": "SBER",
            "board": "TQBR",
            "currency": "RUB",
            "quantity": 10,
            "currentPrice": 244.50,
            "currentValue": 2445.00,
            "unrealizedPL": 120.50,
            "unrealizedPercentPL": 5.18,
            "dailyPL": 12.00,
            "isBlocked": false
          },
          {
            "type": "CURRENCY",
            "ticker": "USD000UTSTOM",
            "board": "CETS",
            "currency": "RUB",
            "quantity": 100
          }
        ]
        """;

        var positions = JsonSerializer.Deserialize<BcsPortfolioItem[]>(payload, Options);

        Assert.NotNull(positions);
        Assert.Equal(2, positions!.Length);

        var sber = positions[0];
        Assert.Equal("STOCK", sber.Type);
        Assert.Equal("SBER", sber.Ticker);
        Assert.Equal("TQBR", sber.Board);
        Assert.Equal(10m, sber.Quantity);
        Assert.Equal(2445.00m, sber.CurrentValue);
        Assert.Equal(120.50m, sber.UnrealizedPL);
        Assert.Equal(5.18m, sber.UnrealizedPercentPL);
        Assert.Equal(12.00m, sber.DailyPL);
        Assert.False(sber.IsBlocked);

        var usd = positions[1];
        Assert.Equal("CURRENCY", usd.Type);
        Assert.Equal(100m, usd.Quantity);
    }

    [Fact]
    public void EmptyPortfolioStreamMessage_Deserializes_AsEmptyArray()
    {
        var positions = JsonSerializer.Deserialize<BcsPortfolioItem[]>("[]", Options);

        Assert.NotNull(positions);
        Assert.Empty(positions!);
    }
}

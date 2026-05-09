namespace Bcs.InvestApi.Tests.Serialization;

using System.Text.Json;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Instruments;
using Bcs.InvestApi.Limits;
using Bcs.InvestApi.Portfolio;
using Xunit;

public sealed class BcsDtoNullabilityTests
{
    [Fact]
    public void BcsInstrument_MissingScalarFields_DeserializesAsNull()
    {
        const string json = """
        {
          "ticker": "SBER",
          "instrumentType": "STOCK"
        }
        """;

        var instrument = JsonSerializer.Deserialize<BcsInstrument>(json, BcsJson.SerializerOptions);

        Assert.NotNull(instrument);
        Assert.Null(instrument.FaceValue);
        Assert.Null(instrument.Scale);
        Assert.Null(instrument.MinimumStep);
        Assert.Null(instrument.IsBlocked);
        Assert.Null(instrument.LotSize);
    }

    [Fact]
    public void BcsLimitsResponse_MissingScalarFields_DeserializesAsNull()
    {
        const string json = """
        {
          "depoLimit": [
            {
              "ticker": "SBER",
              "quantity": {}
            }
          ],
          "futureHolding": [
            {
              "ticker": "SiM6"
            }
          ],
          "moneyLimits": [
            {
              "currencyCode": "RUB"
            }
          ],
          "futuresLimits": [
            {
              "currencyCode": "RUB"
            }
          ]
        }
        """;

        var limits = JsonSerializer.Deserialize<BcsLimitsResponse>(json, BcsJson.SerializerOptions);

        Assert.NotNull(limits);

        var depoLimit = Assert.Single(limits.DepoLimit);
        Assert.Null(depoLimit.AveragePrice);
        Assert.Null(depoLimit.LoadDate);
        Assert.NotNull(depoLimit.Quantity);
        Assert.Null(depoLimit.Quantity.Value);

        var futureHolding = Assert.Single(limits.FutureHolding);
        Assert.Null(futureHolding.CbplPlanned);
        Assert.Null(futureHolding.ExecutionDate);
        Assert.Null(futureHolding.TradeDate);

        var moneyLimit = Assert.Single(limits.MoneyLimits);
        Assert.Null(moneyLimit.Locked);
        Assert.Null(moneyLimit.AveragePrice);
        Assert.Null(moneyLimit.LoadDate);

        var futuresLimit = Assert.Single(limits.FuturesLimits);
        Assert.Null(futuresLimit.AccruedInt);
        Assert.Null(futuresLimit.CbpLimit);
        Assert.Null(futuresLimit.LoadDate);
    }

    [Fact]
    public void BcsPortfolioItem_MissingScalarFields_DeserializesAsNull()
    {
        const string json = """
        {
          "ticker": "SBER",
          "instrumentType": "STOCK"
        }
        """;

        var item = JsonSerializer.Deserialize<BcsPortfolioItem>(json, BcsJson.SerializerOptions);

        Assert.NotNull(item);
        Assert.Null(item.Quantity);
        Assert.Null(item.FaceValue);
        Assert.Null(item.Scale);
        Assert.Null(item.IsBlocked);
    }
}

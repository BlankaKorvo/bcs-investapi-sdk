namespace Bcs.InvestApi.Tests.Serialization;

using System.Text.Json;
using Bcs.InvestApi.DTO;
using Bcs.InvestApi.Infrastructure;
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

    [Fact]
    public void BcsCandlesResponse_MissingScalarFields_DeserializesAsNull()
    {
        const string json = """
        {
          "ticker": "SBER",
          "bars": [
            {
            }
          ]
        }
        """;

        var candles = JsonSerializer.Deserialize<BcsCandlesResponse>(json, BcsJson.SerializerOptions);

        Assert.NotNull(candles);
        Assert.Null(candles.StartDate);
        Assert.Null(candles.EndDate);

        var bar = Assert.Single(candles.Bars);
        Assert.Null(bar.Time);
        Assert.Null(bar.Open);
        Assert.Null(bar.Close);
        Assert.Null(bar.High);
        Assert.Null(bar.Low);
        Assert.Null(bar.Volume);
    }

    [Fact]
    public void BcsDailyTradingScheduleResponse_MissingScalarFields_DeserializesAsNull()
    {
        const string json = """
        {
          "dailySchedule": [
            {
              "tradingSessionStatus": "OPEN"
            }
          ]
        }
        """;

        var schedule = JsonSerializer.Deserialize<BcsDailyTradingScheduleResponse>(json, BcsJson.SerializerOptions);

        Assert.NotNull(schedule);
        Assert.Null(schedule.IsWorkDay);

        var entry = Assert.Single(schedule.DailySchedule);
        Assert.Null(entry.StartDate);
        Assert.Null(entry.EndDate);
    }

    [Fact]
    public void BcsLimitsResponse_NonstandardAcronymFields_DeserializeFromWireNames()
    {
        const string json = """
        {
          "futureHolding": [
            {
              "cbplPlanned": 11.2
            }
          ],
          "futuresLimits": [
            {
              "accruedint": 1.1,
              "cbpLimit": 2.2,
              "cbplUsed": 3.3,
              "cbplPlanned": 4.4,
              "cbplUsedForOrders": 5.5,
              "cbplUsedForPositions": 6.6
            }
          ]
        }
        """;

        var limits = JsonSerializer.Deserialize<BcsLimitsResponse>(json, BcsJson.SerializerOptions);

        Assert.NotNull(limits);
        Assert.Equal(11.2m, Assert.Single(limits.FutureHolding).CbplPlanned);

        var futuresLimit = Assert.Single(limits.FuturesLimits);
        Assert.Equal(1.1m, futuresLimit.AccruedInt);
        Assert.Equal(2.2m, futuresLimit.CbpLimit);
        Assert.Equal(3.3m, futuresLimit.CbplUsed);
        Assert.Equal(4.4m, futuresLimit.CbplPlanned);
        Assert.Equal(5.5m, futuresLimit.CbplUsedForOrders);
        Assert.Equal(6.6m, futuresLimit.CbplUsedForPositions);
    }

    [Fact]
    public void BcsLimitsResponse_PostmanSnippet_Deserializes()
    {
        var limits = DeserializePostmanOkResponse<BcsLimitsResponse>("trade-api-bff-limit/api/v1/limits");

        Assert.Equal(5, limits.DepoLimit.Count);

        var depoLimit = limits.DepoLimit[0];
        Assert.Equal("APTK", depoLimit.Ticker);
        Assert.Equal("TQBR", depoLimit.ClassCode);
        Assert.Equal(8.476222m, depoLimit.AveragePrice);
        Assert.Equal(-90m, depoLimit.Quantity?.Value);
        Assert.Equal(new DateTimeOffset(2026, 2, 2, 21, 0, 0, TimeSpan.Zero), depoLimit.LoadDate);

        Assert.Empty(limits.FutureHolding);
        Assert.Equal(3, limits.MoneyLimits.Count);
        Assert.Empty(limits.FuturesLimits);
    }

    [Fact]
    public void BcsPortfolioItem_PostmanSnippet_Deserializes()
    {
        var portfolio = DeserializePostmanOkResponse<List<BcsPortfolioItem>>(
            "trade-api-bff-portfolio/api/v1/portfolio");

        Assert.NotEmpty(portfolio);

        var item = portfolio[0];
        Assert.Equal("moneyLimit", item.Type);
        Assert.Equal("RUB", item.Ticker);
        Assert.Equal(1061.32m, item.Quantity);
        Assert.Equal(0m, item.UnrealizedPL);
        Assert.Equal(0m, item.UnrealizedPercentPL);
        Assert.Equal(0m, item.DailyPL);
        Assert.Equal(0m, item.DailyPercentPL);
        Assert.False(item.IsBlocked);
    }

    [Fact]
    public void BcsDailyTradingScheduleResponse_PostmanSnippet_Deserializes()
    {
        var schedule = DeserializePostmanOkResponse<BcsDailyTradingScheduleResponse>(
            "trading-schedule/daily-schedule");

        Assert.Equal(true, schedule.IsWorkDay);

        var entry = schedule.DailySchedule[0];
        Assert.Equal(new TimeOnly(16, 5, 0), entry.StartDate);
        Assert.Equal(new TimeOnly(20, 50, 0), entry.EndDate);
        Assert.Equal("OPEN", entry.TradingSessionStatus);
    }

    [Fact]
    public void BcsCandlesResponse_PostmanSnippet_Deserializes()
    {
        var candles = DeserializePostmanOkResponse<BcsCandlesResponse>("candles-chart");

        Assert.Equal("GAZP", candles.Ticker);
        Assert.Equal("TQBR", candles.ClassCode);
        Assert.Equal(new DateTimeOffset(2025, 7, 16, 3, 59, 0, TimeSpan.Zero), candles.StartDate);
        Assert.Equal(new DateTimeOffset(2025, 7, 16, 20, 49, 0, TimeSpan.Zero), candles.EndDate);

        var bar = candles.Bars[0];
        Assert.Equal(new DateTimeOffset(2025, 7, 16, 20, 49, 0, TimeSpan.Zero), bar.Time);
        Assert.Equal(122.68m, bar.Open);
        Assert.Equal(122.83m, bar.Close);
        Assert.Equal(122.83m, bar.High);
        Assert.Equal(122.63m, bar.Low);
        Assert.Equal(9030444.7m, bar.Volume);
    }

    [Fact]
    public void BcsInstrument_PostmanSnippet_Deserializes()
    {
        var instruments = DeserializePostmanOkResponse<List<BcsInstrument>>("instruments/by-isins");

        var instrument = Assert.Single(instruments, instrument => instrument.BcsScore == 4);
        Assert.Equal("GAZP", instrument.Ticker);
        Assert.Equal("RU0007661625", instrument.Isin);
        Assert.Equal(0m, instrument.AccruedInt);
        Assert.Equal(4, instrument.BcsScore);
        Assert.Equal("yellow", instrument.BcsScoreColor);
        Assert.Equal(3030209651200m, instrument.Mktcap);
        Assert.False(instrument.AmortisedMty);
    }

    private static T DeserializePostmanOkResponse<T>(string urlFragment)
    {
        var json = GetPostmanOkResponseBody(urlFragment);
        return JsonSerializer.Deserialize<T>(json, BcsJson.SerializerOptions)
            ?? throw new JsonException($"Postman response for '{urlFragment}' deserialized to null.");
    }

    private static string GetPostmanOkResponseBody(string urlFragment)
    {
        using var collection = JsonDocument.Parse(File.ReadAllText(FindPostmanCollectionPath()));

        foreach (var item in EnumerateItems(collection.RootElement.GetProperty("item")))
        {
            if (!TryGetRawUrl(item, out var rawUrl)
                || !rawUrl.Contains(urlFragment, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!item.TryGetProperty("response", out var responses))
            {
                continue;
            }

            foreach (var response in responses.EnumerateArray())
            {
                if (response.TryGetProperty("code", out var code)
                    && code.GetInt32() == 200
                    && response.TryGetProperty("body", out var body))
                {
                    return body.GetString()
                        ?? throw new JsonException($"Postman response for '{urlFragment}' has a null body.");
                }
            }
        }

        throw new InvalidOperationException($"200 OK Postman response was not found for '{urlFragment}'.");
    }

    private static IEnumerable<JsonElement> EnumerateItems(JsonElement items)
    {
        foreach (var item in items.EnumerateArray())
        {
            yield return item;

            if (!item.TryGetProperty("item", out var childItems))
            {
                continue;
            }

            foreach (var childItem in EnumerateItems(childItems))
            {
                yield return childItem;
            }
        }
    }

    private static bool TryGetRawUrl(JsonElement item, out string rawUrl)
    {
        rawUrl = string.Empty;
        if (!item.TryGetProperty("request", out var request)
            || !request.TryGetProperty("url", out var url)
            || !url.TryGetProperty("raw", out var raw))
        {
            return false;
        }

        rawUrl = raw.GetString() ?? string.Empty;
        return rawUrl.Length > 0;
    }

    private static string FindPostmanCollectionPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "HTTP.postman_collection.json");
            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("HTTP.postman_collection.json was not found.");
    }
}

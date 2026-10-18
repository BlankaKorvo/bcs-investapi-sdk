namespace Bcs.InvestApi.Tests.Serialization;

using System.Text.Json;
using Bcs.InvestApi.Contracts.MarketData;
using Bcs.InvestApi.Contracts.Orders;
using Bcs.InvestApi.Contracts.TradingSchedule;
using Bcs.InvestApi.Infrastructure;
using Xunit;

/// <summary>
/// A BCS <c>date-time</c> is RFC 3339: the offset is required. Without it the SDK does not take the machine's time zone for
/// it — the answer does not parse.
/// </summary>
public sealed class BcsDateTimeOffsetTests
{
    [Theory]
    [InlineData("2026-10-19T07:00:00Z", 7)]
    [InlineData("2026-10-19T07:00:00.123Z", 7)]
    [InlineData("2026-10-19T10:00:00+03:00", 7)]
    [InlineData("2026-10-19T02:00:00-05:00", 7)]
    public void WithAnOffset_IsTheInstantItNames(string value, int utcHour)
    {
        var status = Deserialize<BcsTradingScheduleStatusResponse>($$"""{ "nextSessionDate": "{{value}}" }""");

        Assert.Equal(utcHour, status.NextSessionDate!.Value.UtcDateTime.Hour);
        Assert.Equal(new DateTime(2026, 10, 19), status.NextSessionDate.Value.UtcDateTime.Date);
    }

    [Theory]
    [InlineData("2026-10-19T07:00:00")]
    [InlineData("2026-10-19T07:00:00.123")]
    [InlineData("2026-10-19")]
    public void WithoutAnOffset_DoesNotParse(string value)
    {
        var error = Assert.Throws<JsonException>(() =>
            Deserialize<BcsTradingScheduleStatusResponse>($$"""{ "nextSessionDate": "{{value}}" }"""));

        Assert.Contains(value, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryDateTimeOfTheSdk_IsStrict_TheCandleTimeToo()
    {
        Assert.Throws<JsonException>(() =>
            Deserialize<BcsCandlesResponse>("""{ "bars": [ { "time": "2026-10-19T07:00:00" } ] }"""));

        var candles = Deserialize<BcsCandlesResponse>("""{ "bars": [ { "time": "2026-10-19T07:00:00Z" } ] }""");
        Assert.Equal(new DateTimeOffset(2026, 10, 19, 7, 0, 0, TimeSpan.Zero), candles.Bars[0].Time);
    }

    [Fact]
    public void NotAString_DoesNotParse()
    {
        Assert.Throws<JsonException>(() => Deserialize<BcsTradingScheduleStatusResponse>("""{ "nextSessionDate": 1778329228690 }"""));
    }

    [Fact]
    public void Missing_OrNull_IsNull()
    {
        Assert.Null(Deserialize<BcsTradingScheduleStatusResponse>("{}").NextSessionDate);
        Assert.Null(Deserialize<BcsTradingScheduleStatusResponse>("""{ "nextSessionDate": null }""").NextSessionDate);
    }

    [Fact]
    public void Written_WithItsOffset()
    {
        var json = JsonSerializer.Serialize(
            new BcsOrdersSearchRequest { StartDateTime = new DateTimeOffset(2026, 10, 19, 10, 0, 0, TimeSpan.FromHours(3)) },
            BcsJson.SerializerOptions);

        Assert.Contains("\"2026-10-19T10:00:00+03:00\"", json, StringComparison.Ordinal);
    }

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, BcsJson.SerializerOptions)
        ?? throw new InvalidOperationException($"{typeof(T).Name} deserialized to null.");
}

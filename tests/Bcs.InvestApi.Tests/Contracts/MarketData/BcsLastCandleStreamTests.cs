namespace Bcs.InvestApi.Tests.Contracts.MarketData;

using System.Text.Json;
using Bcs.InvestApi.Contracts.MarketData;
using Xunit;

public sealed class BcsLastCandleStreamTests
{
    // Mirrors the SDK's transport options (Bcs.InvestApi.Infrastructure.BcsJson).
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void SubscribeRequest_Serializes_ToDocumentedShape()
    {
        var request = new BcsMarketDataSubscribeRequest
        {
            SubscribeType = BcsMarketDataSubscribeType.Subscribe,
            DataType = BcsMarketDataType.Candles,
            TimeFrame = "M1",
            Instruments = new[]
            {
                new BcsMarketDataInstrument { ClassCode = "TQBR", Ticker = "SBER" },
            },
        };

        var json = JsonSerializer.Serialize(request, Options);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(0, root.GetProperty("subscribeType").GetInt32());
        Assert.Equal(1, root.GetProperty("dataType").GetInt32());
        Assert.Equal("M1", root.GetProperty("timeFrame").GetString());
        var instrument = Assert.Single(root.GetProperty("instruments").EnumerateArray());
        Assert.Equal("TQBR", instrument.GetProperty("classCode").GetString());
        Assert.Equal("SBER", instrument.GetProperty("ticker").GetString());
    }

    [Fact]
    public void UnsubscribeRequest_SerializesSubscribeTypeAsOne()
    {
        var request = new BcsMarketDataSubscribeRequest
        {
            SubscribeType = BcsMarketDataSubscribeType.Unsubscribe,
            TimeFrame = "M1",
        };

        var json = JsonSerializer.Serialize(request, Options);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("subscribeType").GetInt32());
    }

    [Fact]
    public void SubscriptionAck_Deserializes()
    {
        const string payload = """
        {
          "responseType": "CandleStickSuccess",
          "subscribeType": 0,
          "ticker": "SBER",
          "classCode": "TQBR",
          "timeFrame": "M1",
          "dateTime": "2024-11-10T10:30:00.000Z"
        }
        """;

        var message = JsonSerializer.Deserialize<BcsLastCandleMessage>(payload, Options);

        Assert.NotNull(message);
        Assert.Equal("CandleStickSuccess", message!.ResponseType);
        Assert.Equal(BcsMarketDataSubscribeType.Subscribe, message.SubscribeType);
        Assert.Equal("SBER", message.Ticker);
        Assert.Equal("TQBR", message.ClassCode);
        Assert.Equal("M1", message.TimeFrame);
        Assert.Equal(
            new DateTimeOffset(2024, 11, 10, 10, 30, 0, TimeSpan.Zero),
            message.DateTime);
        Assert.Empty(message.Errors);
    }

    [Fact]
    public void CandleData_Deserializes()
    {
        const string payload = """
        {
          "responseType": "CandleStick",
          "ticker": "SBER",
          "classCode": "TQBR",
          "timeFrame": "M1",
          "open": 244.20,
          "close": 244.50,
          "high": 244.70,
          "low": 243.90,
          "volume": 3200,
          "dateTime": "2024-11-10T10:30:00.000Z"
        }
        """;

        var message = JsonSerializer.Deserialize<BcsLastCandleMessage>(payload, Options);

        Assert.NotNull(message);
        Assert.Equal("CandleStick", message!.ResponseType);
        Assert.Equal(244.20m, message.Open);
        Assert.Equal(244.50m, message.Close);
        Assert.Equal(244.70m, message.High);
        Assert.Equal(243.90m, message.Low);
        Assert.Equal(3200m, message.Volume);
        Assert.Empty(message.Errors);
    }

    [Fact]
    public void ErrorMessage_Deserializes()
    {
        const string payload = """
        {
          "responseType": "CandleStick",
          "errors": [
            {
              "message": "Input JSON structure does not match structure, 'timeFrame' field is undefined.",
              "code": "INCORRECT_JSON"
            }
          ]
        }
        """;

        var message = JsonSerializer.Deserialize<BcsLastCandleMessage>(payload, Options);

        Assert.NotNull(message);
        var error = Assert.Single(message!.Errors);
        Assert.Equal("INCORRECT_JSON", error.Code);
        Assert.Contains("timeFrame", error.Message);
        Assert.Null(message.Open);
    }
}

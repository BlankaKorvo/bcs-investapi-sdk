namespace Bcs.InvestApi.Tests.Contracts.MarketData;

using System.Text.Json;
using Bcs.InvestApi.Contracts.MarketData;
using Xunit;

public sealed class BcsLastTradesStreamTests
{
    // Mirrors the SDK's transport options (Bcs.InvestApi.Infrastructure.BcsJson).
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void SubscribeRequest_Serializes_ToDocumentedShape()
    {
        var request = new BcsLastTradesSubscribeRequest
        {
            SubscribeType = BcsMarketDataSubscribeType.Subscribe,
            DataType = BcsMarketDataType.AnonymousTrades,
            Instruments = new[]
            {
                new BcsMarketDataInstrument { Ticker = "SBER", ClassCode = "TQBR" },
            },
        };

        var json = JsonSerializer.Serialize(request, Options);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(0, root.GetProperty("subscribeType").GetInt32());
        Assert.Equal(2, root.GetProperty("dataType").GetInt32());
        var instrument = Assert.Single(root.GetProperty("instruments").EnumerateArray());
        Assert.Equal("TQBR", instrument.GetProperty("classCode").GetString());
        Assert.Equal("SBER", instrument.GetProperty("ticker").GetString());
    }

    [Fact]
    public void UnsubscribeRequest_SerializesSubscribeTypeAsOne()
    {
        var request = new BcsLastTradesSubscribeRequest
        {
            SubscribeType = BcsMarketDataSubscribeType.Unsubscribe,
        };

        var json = JsonSerializer.Serialize(request, Options);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("subscribeType").GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("dataType").GetInt32());
    }

    [Fact]
    public void SubscriptionAck_Deserializes()
    {
        const string payload = """
        {
          "responseType": "LastTradesSuccess",
          "subscribeType": 0,
          "ticker": "SBER",
          "classCode": "TQBR",
          "dateTime": "2024-11-01T10:00:00.000Z"
        }
        """;

        var message = JsonSerializer.Deserialize<BcsLastTradeMessage>(payload, Options);

        Assert.NotNull(message);
        Assert.Equal("LastTradesSuccess", message!.ResponseType);
        Assert.Equal(BcsMarketDataSubscribeType.Subscribe, message.SubscribeType);
        Assert.Equal("SBER", message.Ticker);
        Assert.Equal("TQBR", message.ClassCode);
        Assert.Empty(message.Errors);
    }

    [Fact]
    public void TradeData_Deserializes()
    {
        const string payload = """
        {
          "responseType": "LastTrades",
          "ticker": "SBER",
          "classCode": "TQBR",
          "side": "BUY",
          "volume": 612,
          "price": 305.89,
          "quantity": 2,
          "dateTime": "2025-11-20T06:29:41.000Z"
        }
        """;

        var message = JsonSerializer.Deserialize<BcsLastTradeMessage>(payload, Options);

        Assert.NotNull(message);
        Assert.Equal("LastTrades", message!.ResponseType);
        Assert.Equal("SBER", message.Ticker);
        Assert.Equal("BUY", message.Side);
        Assert.Equal(612m, message.Volume);
        Assert.Equal(305.89m, message.Price);
        Assert.Equal(2m, message.Quantity);
        Assert.Empty(message.Errors);
    }

    [Fact]
    public void ErrorMessage_Deserializes()
    {
        const string payload = """
        {
          "responseType": "LastTrades",
          "errors": [
            {
              "message": "Input JSON structure does not match structure, 'instruments' field is undefined.",
              "code": "INCORRECT_JSON"
            }
          ]
        }
        """;

        var message = JsonSerializer.Deserialize<BcsLastTradeMessage>(payload, Options);

        Assert.NotNull(message);
        var error = Assert.Single(message!.Errors);
        Assert.Equal("INCORRECT_JSON", error.Code);
        Assert.Null(message.Price);
    }
}

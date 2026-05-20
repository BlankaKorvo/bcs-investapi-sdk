namespace Bcs.InvestApi.Tests.Contracts.MarketData;

using System.Text.Json;
using Bcs.InvestApi.Contracts.MarketData;
using Xunit;

public sealed class BcsOrderBookStreamTests
{
    // Mirrors the SDK's transport options (Bcs.InvestApi.Infrastructure.BcsJson).
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void SubscribeRequest_Serializes_ToDocumentedShape()
    {
        var request = new BcsOrderBookSubscribeRequest
        {
            SubscribeType = BcsMarketDataSubscribeType.Subscribe,
            DataType = BcsMarketDataType.OrderBook,
            Depth = 20,
            Instruments = new[]
            {
                new BcsMarketDataInstrument { Ticker = "SBER", ClassCode = "TQBR" },
            },
        };

        var json = JsonSerializer.Serialize(request, Options);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(0, root.GetProperty("subscribeType").GetInt32());
        Assert.Equal(0, root.GetProperty("dataType").GetInt32());
        Assert.Equal(20, root.GetProperty("depth").GetInt32());
        var instrument = Assert.Single(root.GetProperty("instruments").EnumerateArray());
        Assert.Equal("TQBR", instrument.GetProperty("classCode").GetString());
        Assert.Equal("SBER", instrument.GetProperty("ticker").GetString());
    }

    [Fact]
    public void UnsubscribeRequest_SerializesSubscribeTypeAsOne()
    {
        var request = new BcsOrderBookSubscribeRequest
        {
            SubscribeType = BcsMarketDataSubscribeType.Unsubscribe,
        };

        var json = JsonSerializer.Serialize(request, Options);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("subscribeType").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("dataType").GetInt32());
    }

    [Fact]
    public void SubscriptionAck_Deserializes()
    {
        const string payload = """
        {
          "responseType": "OrderBookSuccess",
          "subscribeType": 0,
          "ticker": "SBER",
          "classCode": "TQBR",
          "depth": 20,
          "dateTime": "2024-10-30T09:01:00.000Z"
        }
        """;

        var message = JsonSerializer.Deserialize<BcsOrderBookMessage>(payload, Options);

        Assert.NotNull(message);
        Assert.Equal("OrderBookSuccess", message!.ResponseType);
        Assert.Equal(BcsMarketDataSubscribeType.Subscribe, message.SubscribeType);
        Assert.Equal("SBER", message.Ticker);
        Assert.Equal("TQBR", message.ClassCode);
        Assert.Equal(20, message.Depth);
        Assert.Empty(message.Bids);
        Assert.Empty(message.Asks);
        Assert.Empty(message.Errors);
    }

    [Fact]
    public void OrderBookData_Deserializes()
    {
        // bidVolume/askVolume arrive as JSON strings; web number handling parses them.
        const string payload = """
        {
          "responseType": "OrderBook",
          "ticker": "SBER",
          "classCode": "TQBR",
          "depth": 20,
          "dateTime": "2024-10-30T09:01:00.000Z",
          "bidVolume": "59851",
          "askVolume": "90339",
          "bids": [
            { "price": 244.30, "quantity": 100 },
            { "price": 244.25, "quantity": 90 }
          ],
          "asks": [
            { "price": 244.35, "quantity": 120 },
            { "price": 244.40, "quantity": 50 }
          ]
        }
        """;

        var message = JsonSerializer.Deserialize<BcsOrderBookMessage>(payload, Options);

        Assert.NotNull(message);
        Assert.Equal("OrderBook", message!.ResponseType);
        Assert.Equal(59851m, message.BidVolume);
        Assert.Equal(90339m, message.AskVolume);

        Assert.Equal(2, message.Bids.Count);
        Assert.Equal(244.30m, message.Bids[0].Price);
        Assert.Equal(100m, message.Bids[0].Quantity);

        Assert.Equal(2, message.Asks.Count);
        Assert.Equal(244.35m, message.Asks[0].Price);
        Assert.Equal(120m, message.Asks[0].Quantity);

        Assert.Empty(message.Errors);
    }

    [Fact]
    public void ErrorMessage_Deserializes()
    {
        const string payload = """
        {
          "responseType": "OrderBook",
          "errors": [
            {
              "message": "Input JSON structure does not match structure, 'instruments' field is undefined.",
              "code": "INCORRECT_JSON"
            }
          ]
        }
        """;

        var message = JsonSerializer.Deserialize<BcsOrderBookMessage>(payload, Options);

        Assert.NotNull(message);
        var error = Assert.Single(message!.Errors);
        Assert.Equal("INCORRECT_JSON", error.Code);
        Assert.Contains("instruments", error.Message);
        Assert.Empty(message.Bids);
    }
}

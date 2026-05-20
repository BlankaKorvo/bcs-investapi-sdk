namespace Bcs.InvestApi.Tests.Contracts.MarketData;

using System.Text.Json;
using Bcs.InvestApi.Contracts.MarketData;
using Xunit;

public sealed class BcsQuotesStreamTests
{
    // Mirrors the SDK's transport options (Bcs.InvestApi.Infrastructure.BcsJson).
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void SubscribeRequest_Serializes_ToDocumentedShape()
    {
        var request = new BcsQuotesSubscribeRequest
        {
            SubscribeType = BcsMarketDataSubscribeType.Subscribe,
            DataType = BcsMarketDataType.Quotes,
            Instruments = new[]
            {
                new BcsMarketDataInstrument { Ticker = "SBER", ClassCode = "TQBR" },
            },
        };

        var json = JsonSerializer.Serialize(request, Options);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(0, root.GetProperty("subscribeType").GetInt32());
        Assert.Equal(3, root.GetProperty("dataType").GetInt32());
        var instrument = Assert.Single(root.GetProperty("instruments").EnumerateArray());
        Assert.Equal("TQBR", instrument.GetProperty("classCode").GetString());
        Assert.Equal("SBER", instrument.GetProperty("ticker").GetString());
    }

    [Fact]
    public void UnsubscribeRequest_SerializesSubscribeTypeAsOne()
    {
        var request = new BcsQuotesSubscribeRequest
        {
            SubscribeType = BcsMarketDataSubscribeType.Unsubscribe,
        };

        var json = JsonSerializer.Serialize(request, Options);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("subscribeType").GetInt32());
        Assert.Equal(3, doc.RootElement.GetProperty("dataType").GetInt32());
    }

    [Fact]
    public void QuotesData_Deserializes()
    {
        const string payload = """
        {
          "responseType": "Quotes",
          "ticker": "SBER",
          "classCode": "TQBR",
          "type": "refresh",
          "dateTime": "2024-11-01T10:00:00.000Z",
          "securityTradingStatus": 17,
          "currency": "RUB",
          "bid": 305.86,
          "offer": 305.96,
          "open": 304.76,
          "close": 304.75,
          "high": 307.24,
          "low": 304.75,
          "theoreticalPrice": 0,
          "last": 305.97,
          "bidYield": 0,
          "offerYield": 0,
          "change": 1.05,
          "changeRate": 0.34
        }
        """;

        var message = JsonSerializer.Deserialize<BcsQuotesMessage>(payload, Options);

        Assert.NotNull(message);
        Assert.Equal("Quotes", message!.ResponseType);
        Assert.Equal("SBER", message.Ticker);
        Assert.Equal("TQBR", message.ClassCode);
        Assert.Equal("refresh", message.Type);
        Assert.Equal(17, message.SecurityTradingStatus);
        Assert.Equal("RUB", message.Currency);
        Assert.Equal(305.86m, message.Bid);
        Assert.Equal(305.96m, message.Offer);
        Assert.Equal(305.97m, message.Last);
        Assert.Equal(304.76m, message.Open);
        Assert.Equal(304.75m, message.Close);
        Assert.Equal(307.24m, message.High);
        Assert.Equal(304.75m, message.Low);
        Assert.Equal(1.05m, message.Change);
        Assert.Equal(0.34m, message.ChangeRate);
        Assert.Empty(message.Errors);
    }

    [Fact]
    public void ErrorMessage_Deserializes()
    {
        const string payload = """
        {
          "responseType": "Quotes",
          "errors": [
            {
              "message": "Input JSON structure does not match structure, 'ticker' field is undefined.",
              "code": "INCORRECT_JSON"
            }
          ]
        }
        """;

        var message = JsonSerializer.Deserialize<BcsQuotesMessage>(payload, Options);

        Assert.NotNull(message);
        var error = Assert.Single(message!.Errors);
        Assert.Equal("INCORRECT_JSON", error.Code);
        Assert.Contains("ticker", error.Message);
        Assert.Null(message.Last);
    }
}

namespace Bcs.InvestApi.Tests.Contracts.Orders;

using System.Text.Json;
using Bcs.InvestApi.Contracts.Orders;
using Xunit;

public sealed class BcsOrderExecutionStreamTests
{
    // Mirrors the SDK's transport options (Bcs.InvestApi.Infrastructure.BcsJson).
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ExecutionMessage_Deserializes_AsOrderStatusResponse()
    {
        // The order-execution WebSocket pushes the same payload as the HTTP order-status endpoint.
        const string payload = """
        {
          "originalClientOrderId": "517661df-d051-461d-9389-988abf24de4d",
          "clientOrderId": "",
          "data": {
            "messageType": "8",
            "orderStatus": "2",
            "executionType": "2",
            "orderQuantity": 100,
            "executedQuantity": 100,
            "lastQuantity": 50,
            "remainedQuantity": 0,
            "ticker": "SBER",
            "classCode": "TQBR",
            "side": "1",
            "orderType": "2",
            "averagePrice": 244.5,
            "orderId": "20241030-TQBR-12345678910",
            "executionId": "TQBR-Z3fE7c-S-1-1-N",
            "price": 244.5,
            "currency": "RUB",
            "clientCode": "123456",
            "transactionTime": "2024-10-30T09:01:00.000Z",
            "tradeDate": "2024-10-30",
            "orderNumber": "12345678910",
            "accruedCoupon": 0,
            "executionValue": 24450,
            "commission": 12.3,
            "securityExchange": "TQBR"
          }
        }
        """;

        var message = JsonSerializer.Deserialize<BcsOrderStatusResponse>(payload, Options);

        Assert.NotNull(message);
        Assert.Equal("517661df-d051-461d-9389-988abf24de4d", message!.OriginalClientOrderId);
        Assert.Equal(string.Empty, message.ClientOrderId);

        var data = message.Data;
        Assert.NotNull(data);
        Assert.Equal("8", data!.MessageType);
        Assert.Equal("2", data.OrderStatus);
        Assert.Equal("2", data.ExecutionType);
        Assert.Equal(100m, data.OrderQuantity);
        Assert.Equal(100m, data.ExecutedQuantity);
        Assert.Equal(50m, data.LastQuantity);
        Assert.Equal(0m, data.RemainedQuantity);
        Assert.Equal("SBER", data.Ticker);
        Assert.Equal("TQBR", data.ClassCode);
        Assert.Equal("1", data.Side);
        Assert.Equal("2", data.OrderType);
        Assert.Equal(244.5m, data.AveragePrice);
        Assert.Equal(244.5m, data.Price);
        Assert.Equal("RUB", data.Currency);
        Assert.Equal(new DateOnly(2024, 10, 30), data.TradeDate);
        Assert.Equal("12345678910", data.OrderNumber);
        Assert.Equal(24450m, data.ExecutionValue);
        Assert.Equal(12.3m, data.Commission);
        Assert.Equal("TQBR", data.SecurityExchange);
    }

    [Fact]
    public void TransactionMessage_Deserializes_AsOrderStatusResponse()
    {
        // The order-transaction WebSocket pushes the same payload as the HTTP order-status endpoint.
        const string payload = """
        {
          "originalClientOrderId": "517661df-d051-461d-9389-988abf24de4d",
          "clientOrderId": "",
          "data": {
            "messageType": "8",
            "orderStatus": "1",
            "executionType": "12",
            "orderQuantity": 100,
            "executedQuantity": 50,
            "lastQuantity": 50,
            "remainedQuantity": 50,
            "ticker": "SBER",
            "classCode": "TQBR",
            "side": "1",
            "orderType": "2",
            "averagePrice": 244.5,
            "price": 244.5,
            "currency": "RUB",
            "transactionTime": "2024-10-30T09:01:00.000Z",
            "tradeDate": "2024-10-30",
            "orderNumber": "12345678910",
            "executionValue": 12225,
            "commission": 12.3,
            "securityExchange": "TQBR",
            "rejectReason": "insufficient funds"
          }
        }
        """;

        var message = JsonSerializer.Deserialize<BcsOrderStatusResponse>(payload, Options);

        Assert.NotNull(message);
        Assert.Equal("517661df-d051-461d-9389-988abf24de4d", message!.OriginalClientOrderId);

        var data = message.Data;
        Assert.NotNull(data);
        Assert.Equal("1", data!.OrderStatus);
        Assert.Equal("12", data.ExecutionType);
        Assert.Equal(50m, data.ExecutedQuantity);
        Assert.Equal(50m, data.RemainedQuantity);
        Assert.Equal(12225m, data.ExecutionValue);
        Assert.Equal("insufficient funds", data.RejectReason);
    }

    [Fact]
    public void NullData_Deserializes()
    {
        const string payload = """
        {
          "originalClientOrderId": "517661df-d051-461d-9389-988abf24de4d",
          "clientOrderId": "",
          "data": null
        }
        """;

        var message = JsonSerializer.Deserialize<BcsOrderStatusResponse>(payload, Options);

        Assert.NotNull(message);
        Assert.Null(message!.Data);
    }
}

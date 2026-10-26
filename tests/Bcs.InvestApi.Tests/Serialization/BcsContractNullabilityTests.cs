namespace Bcs.InvestApi.Tests.Serialization;

using System.Text.Json;
using Bcs.InvestApi.Contracts.Enums;
using Bcs.InvestApi.Contracts.Instruments;
using Bcs.InvestApi.Contracts.Limits;
using Bcs.InvestApi.Contracts.MarketData;
using Bcs.InvestApi.Contracts.Orders;
using Bcs.InvestApi.Contracts.Portfolio;
using Bcs.InvestApi.Contracts.TradingSchedule;
using Bcs.InvestApi.Infrastructure;
using Xunit;

public sealed class BcsContractNullabilityTests
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
    public void BcsOrdersSearchResponse_MissingScalarFields_DeserializesAsNull()
    {
        const string json = """
        {
          "records": [
            {
              "ticker": "SBER"
            }
          ]
        }
        """;

        var orders = JsonSerializer.Deserialize<BcsOrdersSearchResponse>(json, BcsJson.SerializerOptions);

        Assert.NotNull(orders);
        Assert.Null(orders.TotalRecords);
        Assert.Null(orders.TotalPages);

        var order = Assert.Single(orders.Records);
        Assert.Equal("SBER", order.Ticker);
        Assert.Null(order.OrderNum);
        Assert.Null(order.OrderStatus);
        Assert.Null(order.TradeDate);
    }

    [Fact]
    public void BcsCreateOrderResponse_MissingScalarFields_DeserializesAsNull()
    {
        const string json = "{}";

        var response = JsonSerializer.Deserialize<BcsCreateOrderResponse>(json, BcsJson.SerializerOptions);

        Assert.NotNull(response);
        Assert.Null(response.ClientOrderId);
        Assert.Null(response.Status);
    }

    [Fact]
    public void BcsOrderStatusResponse_MissingScalarFields_DeserializesAsNull()
    {
        const string json = """
        {
          "data": [
            {
            }
          ]
        }
        """;

        var response = JsonSerializer.Deserialize<BcsOrderStatusResponse>(json, BcsJson.SerializerOptions);

        Assert.NotNull(response);
        Assert.Null(response.ClientOrderId);
        Assert.Null(response.OriginalClientOrderId);

        var data = Assert.Single(response.Data);
        Assert.Null(data.MessageType);
        Assert.Null(data.OrderStatus);
        Assert.Null(data.TransactionTime);
        Assert.Null(data.OrderQuantity);
        Assert.Null(data.TimeInForce);
        Assert.Null(data.Visible);
    }

    // data is required (the method pages): an answer without it does not describe the order.
    [Fact]
    public void BcsOrderStatusResponse_MissingData_IsAnError()
    {
        var exception = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<BcsOrderStatusResponse>("{}", BcsJson.SerializerOptions));

        Assert.Contains("data", exception.Message);
    }

    [Fact]
    public void BcsOrderStatusResponse_DataAsOneObject_MissingScalarFields_DeserializeAsNull()
    {
        var response = JsonSerializer.Deserialize<BcsOrderStatusResponse>("""{ "data": {} }""", BcsJson.SerializerOptions);

        Assert.NotNull(response);
        var data = Assert.Single(response.Data);
        Assert.Null(data.OrderStatus);
        Assert.Null(data.ExecutedQuantity);
        Assert.Null(data.TransactionTime);
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

    // The *_DocumentedExample_Deserializes tests below parse the 200 OK response example
    // ("Example (auto)") from the method's page on https://trade-api.bcs.ru/, verbatim — the
    // site is the only source of truth for the BCS API. The site generates these examples from
    // the documented schema: placeholders like "string" and 0 are expected, and
    // "Unknown Type: date-time" is the generator's text, not data. When the site's schema
    // changes, refresh the example from the page.

    // https://trade-api.bcs.ru/http/limits/ — GET /trade-api-bff-limit/api/v1/limits
    [Fact]
    public void BcsLimitsResponse_DocumentedExample_Deserializes()
    {
        const string json = """
        {
          "depoLimit": [
            {
              "ticker": "string",
              "classCode": "string",
              "exchange": "string",
              "averagePrice": 0,
              "quantity": {
                "type": "T0",
                "value": 0
              },
              "quantityBatch": {
                "type": "T0",
                "value": 0
              },
              "instrumentType": "string",
              "loadDate": "2024-07-29T15:51:28.071Z",
              "lockedBuyValue": 0,
              "lockedSellValue": 0,
              "lockedBuyQuantity": 0,
              "lockedSellQuantity": 0
            }
          ],
          "futureHolding": [
            {
              "ticker": "string",
              "classCode": "string",
              "exchange": "string",
              "cbplPlanned": 0,
              "varMargin": 0,
              "positionValue": 0,
              "totalNet": 0,
              "executionDate": "2024-07-29T15:51:28.071Z",
              "totalVarMargin": 0,
              "realVarMargin": 0,
              "averagePrice": 0,
              "instrumentType": "string",
              "tradeDate": "2024-07-29T15:51:28.071Z"
            }
          ],
          "moneyLimits": [
            {
              "exchange": "string",
              "currencyCode": "string",
              "locked": 0,
              "averagePrice": 0,
              "instrumentType": "string",
              "quantity": {
                "type": "T0",
                "value": 0
              },
              "loadDate": "2024-07-29T15:51:28.071Z"
            }
          ],
          "futuresLimits": [
            {
              "currencyCode": "string",
              "exchange": "string",
              "accruedint": 0,
              "cbpLimit": 0,
              "cbplUsed": 0,
              "cbplPlanned": 0,
              "cbplUsedForOrders": 0,
              "cbplUsedForPositions": 0,
              "optionsPremium": 0,
              "instrumentType": "string",
              "loadDate": "2024-07-29T15:51:28.071Z",
              "varMargin": 0,
              "realVarMargin": 0
            }
          ]
        }
        """;
        var exampleTime = new DateTimeOffset(2024, 7, 29, 15, 51, 28, 71, TimeSpan.Zero);

        var limits = Deserialize<BcsLimitsResponse>(json);

        var depoLimit = Assert.Single(limits.DepoLimit);
        Assert.Equal("string", depoLimit.Ticker);
        Assert.Equal("string", depoLimit.ClassCode);
        Assert.Equal(0m, depoLimit.AveragePrice);
        Assert.Equal("T0", depoLimit.Quantity?.Type);
        Assert.Equal(0m, depoLimit.Quantity?.Value);
        Assert.Equal("T0", depoLimit.QuantityBatch?.Type);
        Assert.Equal(exampleTime, depoLimit.LoadDate);
        Assert.Equal(0m, depoLimit.LockedSellQuantity);

        var futureHolding = Assert.Single(limits.FutureHolding);
        Assert.Equal(0m, futureHolding.CbplPlanned);
        Assert.Equal(exampleTime, futureHolding.ExecutionDate);
        Assert.Equal(exampleTime, futureHolding.TradeDate);

        var moneyLimit = Assert.Single(limits.MoneyLimits);
        Assert.Equal("string", moneyLimit.CurrencyCode);
        Assert.Equal(0m, moneyLimit.Locked);
        Assert.Equal("T0", moneyLimit.Quantity?.Type);
        Assert.Equal(exampleTime, moneyLimit.LoadDate);

        var futuresLimit = Assert.Single(limits.FuturesLimits);
        Assert.Equal(0m, futuresLimit.AccruedInt);
        Assert.Equal(0m, futuresLimit.CbplUsedForPositions);
        Assert.Equal(0m, futuresLimit.RealVarMargin);
        Assert.Equal(exampleTime, futuresLimit.LoadDate);
    }

    // https://trade-api.bcs.ru/http/portfolio/ — GET /trade-api-bff-portfolio/api/v1/portfolio
    [Fact]
    public void BcsPortfolioItem_DocumentedExample_Deserializes()
    {
        const string json = """
        [
          {
            "type": "moneyLimit",
            "account": "string",
            "exchange": "string",
            "ticker": "string",
            "displayName": "string",
            "baseAssetTicker": "string",
            "currency": "string",
            "upperType": "CURRENCY",
            "instrumentType": "string",
            "term": "T0",
            "quantity": 0,
            "locked": 0,
            "balancePrice": 0,
            "currentPrice": 0,
            "balanceValue": 0,
            "balanceValueRub": 0,
            "balanceValueUsd": 0,
            "balanceValueEur": 0,
            "currentValue": 0,
            "currentValueRub": 0,
            "currentValueUsd": 0,
            "currentValueEur": 0,
            "unrealizedPL": 0,
            "unrealizedPercentPL": 0,
            "dailyPL": 0,
            "dailyPercentPL": 0,
            "portfolioShare": 0,
            "scale": 0,
            "minimumStep": 0,
            "board": "string",
            "priceUnit": "string",
            "faceValue": 0,
            "accruedIncome": 0,
            "logoLink": "string",
            "isBlocked": true,
            "isBlockedTradeAccount": true,
            "lockedForFutures": 0,
            "ratioQuantity": 0,
            "expireDate": "string"
          }
        ]
        """;

        var portfolio = Deserialize<List<BcsPortfolioItem>>(json);

        var item = Assert.Single(portfolio);
        Assert.Equal("moneyLimit", item.Type);
        Assert.Equal("string", item.Account);
        Assert.Equal("CURRENCY", item.UpperType);
        Assert.Equal("T0", item.Term);
        Assert.Equal(0m, item.Quantity);
        Assert.Equal(0m, item.UnrealizedPL);
        Assert.Equal(0m, item.UnrealizedPercentPL);
        Assert.Equal(0m, item.DailyPL);
        Assert.Equal(0m, item.DailyPercentPL);
        Assert.Equal(0m, item.FaceValue);
        Assert.True(item.IsBlocked);
        Assert.True(item.IsBlockedTradeAccount);
        Assert.Equal(0m, item.RatioQuantity);
        Assert.Equal("string", item.ExpireDate);
    }

    // https://trade-api.bcs.ru/http/information/get-daily-schedule/ —
    // GET /trade-api-information-service/api/v1/trading-schedule/daily-schedule
    [Fact]
    public void BcsDailyTradingScheduleResponse_DocumentedExample_Deserializes()
    {
        const string json = """
        {
          "isWorkDay": true,
          "dailySchedule": [
            {
              "startDate": "14:30:00",
              "endDate": "14:30:00",
              "tradingSessionType": "Технический перерыв QUIK",
              "tradingSessionStatus": "OPEN"
            }
          ]
        }
        """;

        var schedule = Deserialize<BcsDailyTradingScheduleResponse>(json);

        Assert.Equal(true, schedule.IsWorkDay);

        var entry = Assert.Single(schedule.DailySchedule);
        Assert.Equal(new TimeOnly(14, 30, 0), entry.StartDate);
        Assert.Equal(new TimeOnly(14, 30, 0), entry.EndDate);
        Assert.Equal("Технический перерыв QUIK", entry.TradingSessionType);
        Assert.Equal("OPEN", entry.TradingSessionStatus);
    }

    // https://trade-api.bcs.ru/http/market-data/get-candles-chart/ —
    // GET /trade-api-market-data-connector/api/v1/candles-chart
    [Fact]
    public void BcsCandlesResponse_DocumentedExample_Deserializes()
    {
        const string json = """
        {
          "ticker": "SBER",
          "classCode": "TQBR",
          "startDate": "2025-11-14T07:00:00Z",
          "endDate": "2025-11-14T10:00:00Z",
          "timeFrame": "H1",
          "bars": [
            {
              "time": "2024-07-29T15:51:28.071Z",
              "open": 0,
              "close": 0,
              "high": 0,
              "low": 0,
              "volume": 0
            }
          ]
        }
        """;

        var candles = Deserialize<BcsCandlesResponse>(json);

        Assert.Equal("SBER", candles.Ticker);
        Assert.Equal("TQBR", candles.ClassCode);
        Assert.Equal(new DateTimeOffset(2025, 11, 14, 7, 0, 0, TimeSpan.Zero), candles.StartDate);
        Assert.Equal(new DateTimeOffset(2025, 11, 14, 10, 0, 0, TimeSpan.Zero), candles.EndDate);
        Assert.Equal("H1", candles.TimeFrame);

        var bar = Assert.Single(candles.Bars);
        Assert.Equal(new DateTimeOffset(2024, 7, 29, 15, 51, 28, 71, TimeSpan.Zero), bar.Time);
        Assert.Equal(0m, bar.Open);
        Assert.Equal(0m, bar.Close);
        Assert.Equal(0m, bar.High);
        Assert.Equal(0m, bar.Low);
        Assert.Equal(0m, bar.Volume);
    }

    // https://trade-api.bcs.ru/http/information/get-instruments-by-isins/ —
    // POST /trade-api-information-service/api/v1/instruments/by-isins
    [Fact]
    public void BcsInstrument_DocumentedExample_Deserializes()
    {
        const string json = """
        [
          {
            "ticker": "string",
            "boards": [
              {
                "classCode": "TQBR",
                "exchange": "MOEX"
              }
            ],
            "shortName": "string",
            "displayName": "string",
            "type": "CURRENCY",
            "isin": "string",
            "registrationCode": "string",
            "issuerName": "string",
            "tradingCurrency": "string",
            "faceValue": 0,
            "scale": 0,
            "minimumStep": 0,
            "accruedInt": 0,
            "currencyStepPrice": "string",
            "settleCode": "string",
            "instrumentType": "CURRENCY",
            "settlementCurrency": "string",
            "settlementDate": "2024-07-29T15:51:28.071Z",
            "maturityDate": "Unknown Type: date-time",
            "lotSize": 0,
            "isQualifiedOnly": true,
            "isCanShort": true,
            "baseAsset": "string",
            "qualifiedTestId": 0,
            "qualifiedTestIdTm": 0,
            "availableForUnqualified": true,
            "currencyNominal": "string",
            "stepPrice": 0,
            "isBcsProduct": true,
            "couponsPerYear": 0,
            "couponRate": 0,
            "nextCoupon": "2024-07-29T15:51:28.071Z",
            "complexProduct": 0,
            "baseAssetFuture": "string",
            "subType": "string",
            "percentTargetCurrent": 0,
            "businessSector": "string",
            "peNorm": 0,
            "priceTangible": 0,
            "epsGrowthRate": 0,
            "predictedDps": 0,
            "dividendYield": 0,
            "priceChangeYear": 0,
            "targetPrice": 0,
            "mktcap": 0,
            "isBlocked": true,
            "businessSectorId": 0,
            "primaryBoard": "string",
            "secondaryBoards": [
              "string"
            ],
            "isCanMargin": true,
            "isReplacementBond": true,
            "subTitle": "string",
            "couponTypeName": "string",
            "emissionDate": "Unknown Type: date-time",
            "creditRating": "string",
            "liquidityRating": "string",
            "bcsScore": 0,
            "bcsScoreColor": "string",
            "nrdCode": "string",
            "strike": 0,
            "baseAssetSecuritySecCode": "string",
            "baseAssetSecurityClassCode": "string",
            "businessCountry": "string",
            "businessCountryCode": "string",
            "priceChangeHalfYear": 0,
            "priceChangeMonth": 0,
            "priceChangeEarlyYear": 0,
            "firstCurrCode": "string",
            "amortisedMty": true
          }
        ]
        """;

        var instruments = Deserialize<List<BcsInstrument>>(json);

        var instrument = Assert.Single(instruments);
        Assert.Equal("string", instrument.Ticker);
        var board = Assert.Single(instrument.Boards);
        Assert.Equal("TQBR", board.ClassCode);
        Assert.Equal("MOEX", board.Exchange);
        Assert.Equal("CURRENCY", instrument.Type);
        Assert.Equal("CURRENCY", instrument.InstrumentType);
        Assert.Equal("string", instrument.Isin);
        Assert.Equal(0m, instrument.AccruedInt);
        Assert.Equal(0m, instrument.LotSize);
        Assert.Equal("2024-07-29T15:51:28.071Z", instrument.SettlementDate);
        Assert.Equal(0, instrument.BcsScore);
        Assert.Equal("string", instrument.BcsScoreColor);
        Assert.Equal(0m, instrument.Mktcap);
        Assert.Equal("string", Assert.Single(instrument.SecondaryBoards));
        Assert.True(instrument.AmortisedMty);
    }

    // https://trade-api.bcs.ru/http/operations/all-order-list/ —
    // POST /trade-api-bff-order-details/api/v1/orders/search
    [Fact]
    public void BcsOrdersSearchResponse_DocumentedExample_Deserializes()
    {
        const string json = """
        {
          "records": [
            {
              "orderNum": 0,
              "orderId": "string",
              "clientCode": "string",
              "executionDateTime": "2024-07-29T15:51:28.071Z",
              "executedValue": 0,
              "orderDateTime": "2024-07-29T15:51:28.071Z",
              "tradeDate": "2024-07-29",
              "updateDateTime": "2024-07-29T15:51:28.071Z",
              "ticker": "string",
              "classCode": "string",
              "takePrice": 0,
              "stopPrice": 0,
              "price": 0,
              "settlementCurrency": "string",
              "orderQuantity": 0,
              "remainedQuantity": 0,
              "executedQuantity": 0,
              "rejectReason": "string",
              "averagePrice": 0,
              "calculationVolume": 0,
              "contractSum": 0,
              "orderStatus": 1,
              "orderType": 1,
              "side": 1,
              "orderQuantityLots": 0,
              "remainedQuantityLots": 0,
              "executedQuantityLots": 0,
              "linkedOrder": "string",
              "stopOrder": "string",
              "visible": 0,
              "marketTakeProfit": 1,
              "marketStopLoss": 1,
              "positionPriceStop": 0,
              "positionPriceLimit": 0
            }
          ],
          "totalRecords": 0,
          "totalPages": 0
        }
        """;
        var exampleTime = new DateTimeOffset(2024, 7, 29, 15, 51, 28, 71, TimeSpan.Zero);

        var orders = Deserialize<BcsOrdersSearchResponse>(json);

        Assert.Equal(0, orders.TotalRecords);
        Assert.Equal(0, orders.TotalPages);

        var order = Assert.Single(orders.Records);
        Assert.Equal(0, order.OrderNum);
        Assert.Equal("string", order.OrderId);
        Assert.Equal(exampleTime, order.ExecutionDateTime);
        Assert.Equal(new DateOnly(2024, 7, 29), order.TradeDate);
        Assert.Equal(exampleTime, order.UpdateDateTime);
        Assert.Equal(0m, order.OrderQuantity);
        Assert.Equal(BcsOrderStatus.Canceled, order.OrderStatus);
        Assert.Equal(BcsSearchOrderType.Market, order.OrderType);
        Assert.Equal(BcsOrderSide.Buy, order.Side);
        Assert.Equal(0m, order.ExecutedQuantityLots);
        Assert.Equal(1, order.MarketTakeProfit);
        Assert.Equal(0m, order.PositionPriceLimit);
    }

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, BcsJson.SerializerOptions)
            ?? throw new JsonException($"{typeof(T).Name} example deserialized to null.");
}

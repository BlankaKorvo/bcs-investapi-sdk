# Bcs.InvestApi

Thin C# SDK for BCS Trade API.

Bcs.InvestApi is a raw endpoint client: transport, contract types, and auth helper only.

The SDK does not aggregate, normalize, map, paginate, trade, schedule, or interpret data. It is not an application
layer, token daemon, trading system, portfolio model, or strategy framework.

See [ARCHITECTURE.md](ARCHITECTURE.md) for the boundary rules this package is expected to keep.

## Target Framework

```text
net10.0
```

## Package Shape

- `Bcs.InvestApi` contains SDK core: auth helper, in-memory token cache, HTTP transport, raw contract DTOs, and endpoint methods.
- `Bcs.InvestApi.DependencyInjection` contains optional Microsoft.Extensions.DependencyInjection registration.
- SDK core does not depend on `Microsoft.Extensions.*`.

## SDK Boundary

- Endpoint methods perform one BCS server request per call.
- Raw endpoint contract DTOs stay raw. Callers own business logic, domain mapping, aggregation, normalization, pagination loops,
  trading decisions, scheduling, retry policy, persistence, and operational recovery.
- Raw endpoints stay in SDK, including portfolio, because they are direct BCS server endpoints rather than SDK-level
  portfolio modeling.
- No disk token persistence.
- No automatic refresh-token retry policy around the refresh-token grant.
- No strategy/domain models, repositories, stores, backtests, hosted services, or trading systems.

## Features

- Authorization by stable external refresh/bootstrap secret supplied by the host application.
- Runtime access/refresh token cache in memory.
- Lazy access-token refresh before token expiration.
- Internal token manager that authorizes SDK HTTP requests.
- Typed `BcsAuthException` for non-success auth responses.
- Raw endpoint methods for limits, portfolio, trading schedule, instruments, historical candles, orders, trades, and
  marginal instrument discounts.

## Endpoint Catalog

Endpoints are grouped by domain on the client (e.g. `client.Orders.CreateOrderAsync(...)`).

| Method | BCS endpoint |
|---|---|
| `Limits.GetLimitsAsync` | `GET /trade-api-bff-limit/api/v1/limits` |
| `Portfolio.GetPortfolioAsync` | `GET /trade-api-bff-portfolio/api/v1/portfolio` |
| `TradingSchedule.GetDailyTradingScheduleAsync` | `GET /trade-api-information-service/api/v1/trading-schedule/daily-schedule` |
| `TradingSchedule.GetTradingScheduleStatusAsync` | `GET /trade-api-information-service/api/v1/trading-schedule/status` |
| `Instruments.GetInstrumentsByIsinsAsync` | `POST /trade-api-information-service/api/v1/instruments/by-isins` |
| `Instruments.GetInstrumentsByTickersAsync` | `POST /trade-api-information-service/api/v1/instruments/by-tickers` |
| `Instruments.GetInstrumentsByTypeAsync` | `GET /trade-api-information-service/api/v1/instruments/by-type` |
| `MarketData.GetCandlesAsync` | `GET /trade-api-market-data-connector/api/v1/candles-chart` |
| `Orders.CreateOrderAsync` | `POST /trade-api-bff-operations/api/v1/orders` |
| `Orders.GetOrderStatusAsync` | `GET /trade-api-bff-operations/api/v1/orders/{originalClientOrderId}` |
| `Orders.SearchOrdersAsync` | `POST /trade-api-bff-order-details/api/v1/orders/search` |
| `Orders.UpdateOrderAsync` | `POST /trade-api-bff-operations/api/v1/orders/{originalClientOrderId}` |
| `Orders.CancelOrderAsync` | `POST /trade-api-bff-operations/api/v1/orders/{originalClientOrderId}/cancel` |
| `Trades.SearchTradesAsync` | `POST /trade-api-bff-trade-details/api/v1/trades/search` |
| `MarginalIndicators.GetInstrumentsDiscountsAsync` | `GET /trade-api-bff-marginal-indicators/api/v1/instruments-discounts` |

## WebSocket Streams

The SDK provides authenticated WebSocket connection plumbing only. Connection lifecycle - reconnect,
backoff, ping/pong, multiplexing, and message dispatch - belongs to the application layer (see
[ARCHITECTURE.md](ARCHITECTURE.md) scope matrix).

- `WebSockets.ConnectAsync(Uri url, CancellationToken)` opens an authenticated `ClientWebSocket` (fresh Bearer token per call).
- `WebSockets.ConnectAndSubscribeAsync(url, subscribeMessage, ct)` opens the socket and sends the subscribe payload as the first frame (for streams that require an explicit subscribe, e.g. market data).
- `WebSocket.SendJsonMessageAsync(message, ct)` serializes and sends a single text frame with the SDK's JSON options (subscribe/unsubscribe payloads).
- `WebSocket.ReceiveJsonMessageAsync<T>(ct)` reads one logical message; `WebSocket.ReceiveJsonMessagesAsync<T>(ct)` streams them as an `IAsyncEnumerable<T>` until clean close or cancellation.
- `BcsWebSocketEndpoints` exposes known stream URLs (e.g. `BcsWebSocketEndpoints.Limits`).
- Stream payloads reuse the HTTP contract types (e.g. the limits stream yields `BcsLimitsResponse`).

Push stream (no subscribe message required):

```csharp
using var ws = await client.WebSockets.ConnectAsync(BcsWebSocketEndpoints.Limits, ct);
await foreach (var snapshot in ws.ReceiveJsonMessagesAsync<BcsLimitsResponse>(ct))
{
    Handle(snapshot); // enumeration ends on clean close or cancellation
}
```

Market-data stream (connect + subscribe, then unsubscribe):

```csharp
var subscribe = new BcsMarketDataSubscribeRequest
{
    DataType = BcsMarketDataType.Candles,
    TimeFrame = "M1",
    Instruments = new[] { new BcsMarketDataInstrument { ClassCode = "TQBR", Ticker = "SBER" } },
};

using var ws = await client.WebSockets.ConnectAndSubscribeAsync(
    BcsWebSocketEndpoints.MarketData, subscribe, ct);

await foreach (var message in ws.ReceiveJsonMessagesAsync<BcsLastCandleMessage>(ct))
{
    if (message.Errors.Count > 0) { HandleErrors(message.Errors); break; }
    Handle(message);
}

await ws.SendJsonMessageAsync(
    subscribe with { SubscribeType = BcsMarketDataSubscribeType.Unsubscribe }, ct);
```

## Auth Endpoint

```text
POST https://be.broker.ru/trade-api-keycloak/realms/tradeapi/protocol/openid-connect/token
Content-Type: application/x-www-form-urlencoded
```

`BcsInvestApiSettings.AuthUrl` must be an absolute HTTPS URI. Plain HTTP is rejected by default because the SDK sends
refresh tokens to this endpoint. For local HTTP-only tests, set `AllowInsecureHttpForTesting = true` explicitly.

API endpoints are built relative to `BcsInvestApiSettings.BaseUrl`, which defaults to `https://be.broker.ru`.

Form fields:

| Field | Value |
|---|---|
| `client_id` | `trade-api-read` or `trade-api-write` |
| `refresh_token` | current in-memory rotated refresh token when usable, otherwise the configured external refresh/bootstrap secret |
| `grant_type` | `refresh_token` |

## Settings

| Setting | Default | Description |
|---|---:|---|
| `RefreshToken` | required | Stable external refresh/bootstrap secret supplied by the host/application layer. |
| `ClientId` | `BcsAuthClientIds.TradeApiRead` | BCS auth client id enum. Values map to `trade-api-read` or `trade-api-write`. |
| `AuthUrl` | BCS token endpoint | Full Keycloak token endpoint URL. Must be absolute HTTPS unless local insecure HTTP is explicitly allowed. |
| `BaseUrl` | `https://be.broker.ru` | Base URL for BCS HTTP API endpoints. Must be absolute HTTPS unless local insecure HTTP is explicitly allowed. |
| `AllowInsecureHttpForTesting` | `false` | Allows plain HTTP URLs only for explicit local tests. |
| `Timeout` | `null` | Optional HTTP timeout. If `null`, the `HttpClient` default timeout is used. |
| `TokenRefreshSkew` | `5 minutes` | Refresh access token before its actual expiration. |
| `TokenRefreshOperationTimeout` | `60 seconds` | Maximum time allowed for one refresh-token auth exchange. |

## Authorization Behavior

The SDK keeps the runtime token pair in private in-memory state. The host supplies one stable external
refresh/bootstrap secret through `BcsInvestApiSettings.RefreshToken` or `BcsInvestApiClientFactory.Create(...)`.

Before each broker API request, the SDK obtains a usable access token internally. The first authorized request uses the
configured external secret. Successful auth updates the in-memory token pair. Later refresh calls use the in-memory
rotated `refresh_token` while it is usable; if it is missing or expires within `TokenRefreshSkew`, the SDK uses the
configured external secret again.

Refresh-token grant failures are propagated as `BcsAuthException`. The SDK does not apply retry policies, Polly,
backoff, disk token persistence, or hidden resilience wrappers around the grant. If the process exits, in-memory token
state is lost.

## Direct Usage

```csharp
using Bcs.InvestApi;
using Bcs.InvestApi.Contracts.Enums;
using Bcs.InvestApi.Contracts.Orders;
using Bcs.InvestApi.Contracts.Trades;

var refreshToken = Environment.GetEnvironmentVariable("BCS_REFRESH_TOKEN")
    ?? throw new InvalidOperationException("BCS_REFRESH_TOKEN is not set.");

await using var client = BcsInvestApiClientFactory.Create(
    refreshToken: refreshToken,
    clientId: BcsAuthClientIds.TradeApiRead);

var limits = await client.Limits.GetLimitsAsync();
```

The following calls are independent direct endpoint examples, not an orchestration flow:

```csharp
using Bcs.InvestApi.Contracts.Enums;
using Bcs.InvestApi.Contracts.Orders;
using Bcs.InvestApi.Contracts.Trades;

var portfolio = await client.Portfolio.GetPortfolioAsync();
var schedule = await client.TradingSchedule.GetDailyTradingScheduleAsync("TQBR", "SBER");
var scheduleStatus = await client.TradingSchedule.GetTradingScheduleStatusAsync("TQBR");
var instruments = await client.Instruments.GetInstrumentsByIsinsAsync(
    new[] { "RU0009029540", "RU0007661625", "RU000A0J2Q06" },
    page: 0,
    size: 50);
var instrumentsByTicker = await client.Instruments.GetInstrumentsByTickersAsync(
    new[] { "SBER", "GAZP", "ROSN" },
    page: 0,
    size: 50);
var stocks = await client.Instruments.GetInstrumentsByTypeAsync(
    BcsInstrumentTypes.Stock,
    page: 0,
    size: 50);
var candles = await client.MarketData.GetCandlesAsync(
    "TQBR",
    "SBER",
    new DateTimeOffset(2025, 11, 14, 7, 0, 0, TimeSpan.Zero),
    new DateTimeOffset(2025, 11, 14, 10, 0, 0, TimeSpan.Zero),
    BcsCandleTimeFrames.Hour1);
var orders = await client.Orders.SearchOrdersAsync(
    new BcsOrdersSearchRequest
    {
        StartDateTime = DateTimeOffset.UtcNow.AddDays(-7),
        EndDateTime = DateTimeOffset.UtcNow,
        OrderStatus = new[] { BcsOrderStatus.Active },
    },
    page: 0,
    size: 50,
    sort: new[] { BcsOrderSort.OrderDateTimeDesc });
var orderStatus = await client.Orders.GetOrderStatusAsync(Guid.Parse("12345678-1234-1234-9adb-123456789876"));
var trades = await client.Trades.SearchTradesAsync(
    new BcsTradesSearchRequest
    {
        StartDateTime = DateTimeOffset.UtcNow.AddDays(-7),
        EndDateTime = DateTimeOffset.UtcNow,
        Tickers = new[] { "SBER" },
        ClassCodes = new[] { "TQBR" },
    },
    page: 0,
    size: 50,
    sort: new[] { BcsTradeSort.TradeDateTimeDesc });
var marginDiscounts = await client.MarginalIndicators.GetInstrumentsDiscountsAsync();

await using var writeClient = BcsInvestApiClientFactory.Create(
    refreshToken: refreshToken,
    clientId: BcsAuthClientIds.TradeApiWrite);

var createResult = await writeClient.Orders.CreateOrderAsync(
    new BcsCreateOrderRequest
    {
        ClientOrderId = Guid.NewGuid(),
        Side = BcsOrderSide.Buy,
        OrderType = BcsOrderType.Limit,
        OrderQuantity = 10,
        Ticker = "SBER",
        ClassCode = "TQBR",
        Price = 250.10m,
    });

var updateResult = await writeClient.Orders.UpdateOrderAsync(
    originalClientOrderId: Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
    request: new BcsUpdateOrderRequest
    {
        ClientOrderId = Guid.NewGuid(),
        OrderQuantity = 10,
        Price = 250.20m,
    });

var cancelResult = await writeClient.Orders.CancelOrderAsync(
    originalClientOrderId: Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
    clientOrderId: Guid.NewGuid());
```

## DI Usage

Install/reference `Bcs.InvestApi.DependencyInjection` only when the host already uses Microsoft.Extensions.DependencyInjection.

```csharp
using Bcs.InvestApi;
using Bcs.InvestApi.Contracts.Enums;

services.AddBcsInvestApiClient(settings =>
{
    settings.RefreshToken = configuration["BCS_REFRESH_TOKEN"];
    settings.ClientId = BcsAuthClientIds.TradeApiRead;
});
```

`AddBcsInvestApiClient` registers the facade and internal token services. Endpoint usage is the same direct method usage
shown above.

## Pagination

SDK requests exactly one page. Instrument lookup, order search, and trade search methods perform one BCS request per
call; callers pass `page` and `size` explicitly and own any multi-page iteration policy. The server owns validation of
supported values and max limits.

```csharp
var instruments = await client.Instruments.GetInstrumentsByIsinsAsync(
    new[] { "RU0009029540", "RU0007661625", "RU000A0J2Q06" },
    page: 0,
    size: 50);
```

When the BCS endpoint needs a base asset ticker, pass `baseAssetTicker`; the SDK forwards it as a query parameter when
provided.

```csharp
var sberOptions = await client.Instruments.GetInstrumentsByTypeAsync(
    BcsInstrumentTypes.Options,
    page: 0,
    size: 50,
    baseAssetTicker: "SBER");
```

## Historical Candles

`MarketData.GetCandlesAsync(...)` calls `GET /trade-api-market-data-connector/api/v1/candles-chart`.

```csharp
using Bcs.InvestApi.Contracts.Enums;

var candles = await client.MarketData.GetCandlesAsync(
    classCode: "TQBR",
    ticker: "SBER",
    startDate: new DateTimeOffset(2025, 11, 14, 7, 0, 0, TimeSpan.Zero),
    endDate: new DateTimeOffset(2025, 11, 14, 10, 0, 0, TimeSpan.Zero),
    timeFrame: BcsCandleTimeFrames.Hour1);
```

BCS allows at most 1440 candles in one request.

## Sample

`sample/Bcs.InvestApi.Sample` is a command-based console for raw endpoint calls. It is not recommended application
architecture and intentionally does not show orchestration, hosting, logging, retries, background services, or domain
workflows.

Show available commands:

```bash
dotnet run --project sample/Bcs.InvestApi.Sample -- help
```

Run one command with the first command-line argument, `BCS_SAMPLE_COMMAND`, or the legacy `BCS_SAMPLE_ENDPOINT`:

```bash
dotnet run --project sample/Bcs.InvestApi.Sample -- limits
dotnet run --project sample/Bcs.InvestApi.Sample -- candles
dotnet run --project sample/Bcs.InvestApi.Sample -- stream-quotes
```

Supported read commands:

```text
smoke
limits
portfolio
instruments-by-isin
instruments-by-ticker
instruments-by-type
candles
schedule
schedule-status
orders
order-status
trades
margin-discounts
```

Supported WebSocket commands:

```text
stream-candles
stream-orderbook
stream-quotes
stream-trades
stream-limits
stream-portfolio
stream-order-executions
stream-order-transactions
stream-margin
```

Trading commands are explicit and use `trade-api-write` by default:

```text
order-create
order-update
order-cancel
```

The sample requires `BCS_REFRESH_TOKEN` for all commands except `help` and does not include real tokens.

Common environment variables:

| Variable | Default | Used by |
|---|---:|---|
| `BCS_SAMPLE_CLIENT_ID` | `read` or `write` for trading commands | all commands |
| `BCS_SAMPLE_CLASS_CODE` | `TQBR` | instrument, market data, schedule, orders, trades, streams |
| `BCS_SAMPLE_TICKER` | `SBER` | instrument, market data, schedule, orders, trades, streams |
| `BCS_SAMPLE_ISINS` | `RU0009029540,RU0007661625,RU000A0J2Q06` | `instruments-by-isin` |
| `BCS_SAMPLE_TICKERS` | `SBER,GAZP,ROSN` | `instruments-by-ticker` |
| `BCS_SAMPLE_PAGE` | `0` | paged HTTP commands |
| `BCS_SAMPLE_SIZE` | `10` | paged HTTP commands |
| `BCS_SAMPLE_SEARCH_FROM` | UTC now minus 7 days | `orders`, `trades` |
| `BCS_SAMPLE_SEARCH_TO` | UTC now | `orders`, `trades` |
| `BCS_SAMPLE_CANDLE_TIMEFRAME` | `Minute1` | `candles`, `stream-candles` |
| `BCS_SAMPLE_CANDLES_FROM` | UTC now minus 1 day | `candles` |
| `BCS_SAMPLE_CANDLES_TO` | UTC now | `candles` |
| `BCS_SAMPLE_INSTRUMENT_TYPE` | `Stock` | `instruments-by-type` |
| `BCS_SAMPLE_BASE_ASSET_TICKER` | unset | `instruments-by-type` |
| `BCS_SAMPLE_STREAM_MESSAGES` | `5` | stream commands |
| `BCS_SAMPLE_STREAM_TIMEOUT_SECONDS` | `30` | stream commands |
| `BCS_SAMPLE_ORDERBOOK_DEPTH` | `20` | `stream-orderbook` |

Order-specific variables:

| Operation | Required | Optional |
|---|---|---|
| Status | `BCS_SAMPLE_ORIGINAL_CLIENT_ORDER_ID` | legacy alias: `BCS_SAMPLE_ORDER_STATUS_CLIENT_ORDER_ID` |
| Create | `BCS_SAMPLE_ORDER_QUANTITY`; `BCS_SAMPLE_ORDER_PRICE` for non-market orders | `BCS_SAMPLE_CLIENT_ORDER_ID`, `BCS_SAMPLE_ORDER_SIDE` (default `Buy`), `BCS_SAMPLE_ORDER_TYPE` (default `Limit`) |
| Update | `BCS_SAMPLE_ORIGINAL_CLIENT_ORDER_ID`, `BCS_SAMPLE_ORDER_QUANTITY` | `BCS_SAMPLE_CLIENT_ORDER_ID`, `BCS_SAMPLE_ORDER_PRICE` |
| Cancel | `BCS_SAMPLE_ORIGINAL_CLIENT_ORDER_ID` | `BCS_SAMPLE_CLIENT_ORDER_ID` |

Legacy create/update/cancel variable aliases from older sample versions are still accepted where practical
(`BCS_SAMPLE_CREATE_*`, `BCS_SAMPLE_UPDATE_*`, `BCS_SAMPLE_CANCEL_*`). Bad env-var values fail fast with a non-zero exit
code before any network call.

## Raw Auth Boundary

`BcsInvestApiClient` does not expose raw auth exchange APIs, token manager APIs, refresh tokens, or access-token lifecycle
controls. Runtime rotated refresh tokens returned by BCS remain an internal SDK detail. Callers should use broker API
methods such as `GetLimitsAsync(...)`, `GetPortfolioAsync(...)`, `GetDailyTradingScheduleAsync(...)`,
`GetTradingScheduleStatusAsync(...)`, `GetInstrumentsBy...Async(...)`, `GetCandlesAsync(...)`, `CreateOrderAsync(...)`,
`GetOrderStatusAsync(...)`, `SearchOrdersAsync(...)`, `UpdateOrderAsync(...)`, `CancelOrderAsync(...)`,
`SearchTradesAsync(...)`, and `GetInstrumentsDiscountsAsync(...)`.

If a diagnostic or low-level raw auth API is needed later, keep it separate from the main facade and make refresh-token
exposure explicit in that API.

## Error Handling

BCS non-success auth responses are exposed as `BcsAuthException`:

```csharp
using Bcs.InvestApi.Contracts.Exceptions;

try
{
    var limits = await client.Limits.GetLimitsAsync();
}
catch (BcsAuthException ex) when (ex.Error == "invalid_grant")
{
    // Decide recovery behavior in the host/application layer.
    throw;
}
```

## HttpClient Ownership

The internal auth transport does not dispose a `HttpClient` passed from outside.

- In DI mode, `IHttpClientFactory` owns transport lifecycle.
- In direct factory mode, `BcsInvestApiClientFactory` creates and disposes its own `HttpClient` when the facade is disposed.
- If a custom `HttpMessageHandler` is passed to the factory, the handler is not disposed by the SDK.

## Build

```bash
dotnet build Bcs.InvestApi.sln
```

## Test

```bash
dotnet test Bcs.InvestApi.sln
```

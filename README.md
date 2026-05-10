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
- Raw endpoint methods for limits, portfolio, daily trading schedule, instruments, and historical candles.

## Endpoint Catalog

| Method | BCS endpoint |
|---|---|
| `GetLimitsAsync` | `GET /trade-api-bff-limit/api/v1/limits` |
| `GetPortfolioAsync` | `GET /trade-api-bff-portfolio/api/v1/portfolio` |
| `GetDailyTradingScheduleAsync` | `GET /trade-api-information-service/api/v1/trading-schedule/daily-schedule` |
| `GetInstrumentsByIsinsAsync` | `POST /trade-api-information-service/api/v1/instruments/by-isins` |
| `GetInstrumentsByTickersAsync` | `POST /trade-api-information-service/api/v1/instruments/by-tickers` |
| `GetInstrumentsByTypeAsync` | `GET /trade-api-information-service/api/v1/instruments/by-type` |
| `GetCandlesAsync` | `GET /trade-api-market-data-connector/api/v1/candles-chart` |

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
using Bcs.InvestApi.DependencyInjection;
using Bcs.InvestApi.Contracts.Enums;

var refreshToken = Environment.GetEnvironmentVariable("BCS_REFRESH_TOKEN")
    ?? throw new InvalidOperationException("BCS_REFRESH_TOKEN is not set.");

await using var client = BcsInvestApiClientFactory.Create(
    refreshToken: refreshToken,
    clientId: BcsAuthClientIds.TradeApiRead);

var limits = await client.GetLimitsAsync();
```

The following calls are independent direct endpoint examples, not an orchestration flow:

```csharp
using Bcs.InvestApi.Contracts.Enums;

var portfolio = await client.GetPortfolioAsync();
var schedule = await client.GetDailyTradingScheduleAsync("TQBR", "SBER");
var instruments = await client.GetInstrumentsByIsinsAsync(
    new[] { "RU0009029540", "RU0007661625", "RU000A0J2Q06" },
    page: 0,
    size: 50);
var instrumentsByTicker = await client.GetInstrumentsByTickersAsync(
    new[] { "SBER", "GAZP", "ROSN" },
    page: 0,
    size: 50);
var stocks = await client.GetInstrumentsByTypeAsync(
    BcsInstrumentTypes.Stock,
    page: 0,
    size: 50);
var candles = await client.GetCandlesAsync(
    "TQBR",
    "SBER",
    new DateTimeOffset(2025, 11, 14, 7, 0, 0, TimeSpan.Zero),
    new DateTimeOffset(2025, 11, 14, 10, 0, 0, TimeSpan.Zero),
    BcsCandleTimeFrames.Hour1);
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

SDK requests exactly one page. Instrument lookup methods perform one BCS request per call; callers pass `page` and
`size` explicitly and own any multi-page iteration policy. The server owns validation of supported values and max
limits.

```csharp
var instruments = await client.GetInstrumentsByIsinsAsync(
    new[] { "RU0009029540", "RU0007661625", "RU000A0J2Q06" },
    page: 0,
    size: 50);
```

When the BCS endpoint needs a base asset ticker, pass `baseAssetTicker`; the SDK forwards it as a query parameter when
provided.

```csharp
var sberOptions = await client.GetInstrumentsByTypeAsync(
    BcsInstrumentTypes.Options,
    page: 0,
    size: 50,
    baseAssetTicker: "SBER");
```

## Historical Candles

`GetCandlesAsync(...)` calls `GET /trade-api-market-data-connector/api/v1/candles-chart`.

```csharp
using Bcs.InvestApi.Contracts.Enums;

var candles = await client.GetCandlesAsync(
    classCode: "TQBR",
    ticker: "SBER",
    startDate: new DateTimeOffset(2025, 11, 14, 7, 0, 0, TimeSpan.Zero),
    endDate: new DateTimeOffset(2025, 11, 14, 10, 0, 0, TimeSpan.Zero),
    timeFrame: BcsCandleTimeFrames.Hour1);
```

BCS allows at most 1440 candles in one request.

## Sample

`sample/Bcs.InvestApi.Sample` is a minimal smoke-test console for raw endpoint calls. It is not recommended application
architecture and intentionally does not show orchestration, hosting, logging, retries, background services, or
portfolio/instrument/candle workflows.

Select one endpoint with the first command-line argument or `BCS_SAMPLE_ENDPOINT`:

```bash
BCS_SAMPLE_ENDPOINT=limits dotnet run --project sample/Bcs.InvestApi.Sample
dotnet run --project sample/Bcs.InvestApi.Sample -- candles
```

Supported values are `limits`, `portfolio`, `candles`, and `instruments-by-ticker`. The sample requires
`BCS_REFRESH_TOKEN` and does not include real tokens.

## Raw Auth Boundary

`BcsInvestApiClient` does not expose raw auth exchange APIs, token manager APIs, refresh tokens, or access-token lifecycle
controls. Runtime rotated refresh tokens returned by BCS remain an internal SDK detail. Callers should use broker API
methods such as `GetLimitsAsync(...)`, `GetPortfolioAsync(...)`, `GetDailyTradingScheduleAsync(...)`,
`GetInstrumentsBy...Async(...)`, and `GetCandlesAsync(...)`.

If a diagnostic or low-level raw auth API is needed later, keep it separate from the main facade and make refresh-token
exposure explicit in that API.

## Error Handling

BCS non-success auth responses are exposed as `BcsAuthException`:

```csharp
using Bcs.InvestApi.Contracts.Exceptions;

try
{
    var limits = await client.GetLimitsAsync();
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

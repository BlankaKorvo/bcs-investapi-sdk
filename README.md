# Bcs.InvestApi

Thin C# SDK for BCS Trade API.

The SDK is a library, not a standalone token daemon. The application layer owns a stable external refresh/bootstrap
secret and passes it through `BcsInvestApiSettings.RefreshToken` or `BcsInvestApiClientFactory.Create(refreshToken, ...)`.

## Target framework

```text
net10.0
```

## Features

- Authorization by stable external refresh/bootstrap secret.
- Runtime access/refresh token cache in memory.
- Lazy refresh before token expiration.
- Internal token manager that authorizes SDK requests automatically.
- Typed `BcsAuthException` for non-success auth responses.
- Typed limits, portfolio, daily trading schedule, instruments-by-ISIN, instruments-by-ticker, instruments-by-type,
  and historical candles endpoints.

## Architecture

The host/application layer owns the stable external refresh/bootstrap secret. SDK core only receives that value as input
when the client is created or resolved from DI.

- The SDK does not own the long-term secret.
- The SDK does not write tokens to disk.
- Runtime `access_token` and rotated `refresh_token` values are stored only in SDK memory.
- If the process exits or crashes, in-memory token state is lost.
- A new process must receive the same stable secret again from the upper layer.
- If a persistent secret store is required, implement it in the host/application layer, not in SDK core.

## Auth endpoint

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
| `refresh_token` | current in-memory rotated refresh token when usable, otherwise the stable refresh/bootstrap secret |
| `grant_type` | `refresh_token` |

## Settings

| Setting | Default | Description |
|---|---:|---|
| `RefreshToken` | required | Stable external refresh/bootstrap secret supplied by the host/application layer. |
| `ClientId` | `trade-api-read` | BCS auth client id: `trade-api-read` or `trade-api-write`. |
| `AuthUrl` | BCS token endpoint | Full Keycloak token endpoint URL. Must be absolute HTTPS unless local insecure HTTP is explicitly allowed. |
| `BaseUrl` | `https://be.broker.ru` | Base URL for BCS HTTP API endpoints. Must be absolute HTTPS unless local insecure HTTP is explicitly allowed. |
| `AllowInsecureHttpForTesting` | `false` | Allows plain HTTP URLs only for explicit local tests. |
| `Timeout` | `null` | Optional HTTP timeout. If `null`, the `HttpClient` default timeout is used. |
| `TokenRefreshSkew` | `5 minutes` | Refresh access token before its actual expiration. |
| `TokenRefreshOperationTimeout` | `60 seconds` | Maximum time allowed for one refresh-token auth exchange. |

## Authorization Behavior

The SDK keeps the runtime token pair in private in-memory state:

- Construction, factory creation and DI resolution validate settings and require `BcsInvestApiSettings.RefreshToken`.
- Before each broker API request, the SDK obtains a usable access token internally.
- On the first authorized request it uses `BcsInvestApiSettings.RefreshToken`.
- After successful authorization it updates the in-memory token pair.
- Next refresh uses the in-memory rotated `refresh_token` while it is non-empty and valid under `TokenRefreshSkew`.
- If the in-memory refresh token is missing or expires within `TokenRefreshSkew`, refresh falls back to `BcsInvestApiSettings.RefreshToken`.
- If the in-memory refresh token is rejected with `invalid_grant`, refresh clears the in-memory token pair and falls
  back once to `BcsInvestApiSettings.RefreshToken` when it is a different token.
- If `BcsInvestApiSettings.RefreshToken` is rejected with `invalid_grant`, the `BcsAuthException` is propagated.
- If the access token expires soon, the next broker API request calls the auth endpoint under a refresh gate.
- Concurrent callers re-check the in-memory token after entering the refresh gate.

## Usage Examples

### Factory

```csharp
using Bcs.InvestApi;
using Bcs.InvestApi.Auth;
using Bcs.InvestApi.Instruments;
using Bcs.InvestApi.MarketData;

var refreshToken = Environment.GetEnvironmentVariable("BCS_REFRESH_TOKEN")
    ?? throw new InvalidOperationException("BCS_REFRESH_TOKEN is not set.");

await using var client = BcsInvestApiClientFactory.Create(
    refreshToken: refreshToken,
    clientId: BcsAuthClientIds.TradeApiRead);

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

### DI

```csharp
using Bcs.InvestApi;
using Bcs.InvestApi.Auth;

services.AddBcsInvestApiClient(settings =>
{
    settings.RefreshToken = configuration["BCS_REFRESH_TOKEN"];
    settings.ClientId = BcsAuthClientIds.TradeApiRead;
});
```

`AddBcsInvestApiClient` registers the facade and internal token services. Callers inject the facade and use broker API
methods; access-token lifecycle is handled by the SDK.

Inject the facade:

```csharp
using Bcs.InvestApi.Portfolio;

public sealed class MyService
{
    private readonly BcsInvestApiClient _client;

    public MyService(BcsInvestApiClient client)
    {
        _client = client;
    }

    public Task<IReadOnlyList<BcsPortfolioPosition>> GetPortfolioAsync(CancellationToken ct) =>
        _client.GetPortfolioAsync(ct);
}
```

### Instruments by ISIN, ticker, and type

Instrument lookup methods request exactly one BCS page. Callers pass `page` and `size` explicitly and own any
multi-page iteration policy.

```csharp
var instruments = await client.GetInstrumentsByIsinsAsync(
    new[] { "RU0009029540", "RU0007661625", "RU000A0J2Q06" },
    page: 0,
    size: 50);
```

```csharp
var instruments = await client.GetInstrumentsByTickersAsync(
    new[] { "SBER", "GAZP", "ROSN" },
    page: 0,
    size: 50);
```

```csharp
using Bcs.InvestApi.Instruments;

var stocks = await client.GetInstrumentsByTypeAsync(
    BcsInstrumentTypes.Stock,
    page: 0,
    size: 50);
```

For `OPTIONS`, BCS requires `baseAssetTicker`.

```csharp
var sberOptions = await client.GetInstrumentsByTypeAsync(
    BcsInstrumentTypes.Options,
    page: 0,
    size: 50,
    baseAssetTicker: "SBER");
```

### Historical candles

`GetCandlesAsync(...)` calls `GET /trade-api-market-data-connector/api/v1/candles-chart`.

```csharp
using Bcs.InvestApi.MarketData;

var candles = await client.GetCandlesAsync(
    classCode: "TQBR",
    ticker: "SBER",
    startDate: new DateTimeOffset(2025, 11, 14, 7, 0, 0, TimeSpan.Zero),
    endDate: new DateTimeOffset(2025, 11, 14, 10, 0, 0, TimeSpan.Zero),
    timeFrame: BcsCandleTimeFrames.Hour1);

foreach (var bar in candles.Bars)
{
    Console.WriteLine($"{bar.Time:O}: O={bar.Open} H={bar.High} L={bar.Low} C={bar.Close} V={bar.Volume}");
}
```

BCS allows at most 1440 candles in one request. The SDK validates this before sending the request.

## Raw Auth Boundary

`BcsInvestApiClient` does not expose raw auth exchange APIs, token manager APIs or access-token lifecycle controls.
Runtime rotated refresh tokens returned by BCS remain an internal SDK detail. Callers should use only broker API methods
such as `GetLimitsAsync(...)`, `GetPortfolioAsync(...)`, `GetDailyTradingScheduleAsync(...)`,
`GetInstrumentsBy...Async(...)` and `GetCandlesAsync(...)`.

If a diagnostic or low-level raw auth API is needed later, keep it separate from the main facade and make the refresh
token exposure explicit in that API.

## Error Handling

BCS `400 invalid_grant` response is exposed as `BcsAuthException`:

```csharp
try
{
    var limits = await client.GetLimitsAsync();
}
catch (BcsAuthException ex) when (ex.Error == "invalid_grant")
{
    Console.WriteLine(ex.ErrorDescription);
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

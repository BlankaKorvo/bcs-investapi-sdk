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
- Optional auto-refresh loop.
- DI integration with singleton `BcsTokenManager`.
- Separate retry policy for auth and normal HTTP calls.
- `IBcsAccessTokenProvider` abstraction for HTTP API clients.
- Typed `BcsAuthException` for non-success auth responses.
- Raw auth request/response DTOs.

## Architecture

The host/application layer owns the stable external refresh/bootstrap secret. SDK core only receives that value as input
when the client is created or resolved from DI.

- The SDK does not own the long-term secret.
- The SDK does not write tokens to disk.
- Runtime `access_token` and rotated `refresh_token` values are stored only in `BcsTokenManager` memory.
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
| `AllowInsecureHttpForTesting` | `false` | Allows plain HTTP auth URLs only for explicit local tests. |
| `Timeout` | `null` | Optional HTTP timeout. If `null`, the `HttpClient` default timeout is used. |
| `AuthRetryAttempts` | `0` | Retry attempts for auth refresh-token exchange after the initial request. |
| `HttpRetryAttempts` | `3` | Retry attempts for idempotent read/query HTTP calls after the initial request. |
| `HttpRetryBaseDelay` | `250ms` | Base delay for exponential retry backoff. |
| `TokenRefreshSkew` | `5 minutes` | Refresh access token before its actual expiration. |
| `AutoRefreshInterval` | `1 minute` | Timer tick interval for the optional auto-refresh loop. |
| `TokenRefreshOperationTimeout` | `60 seconds` | Maximum time allowed for one refresh-token auth exchange. |

## Token Manager Behavior

`BcsTokenManager` keeps the runtime token pair in a private in-memory field:

- Construction, factory creation and DI resolution validate settings and require `BcsInvestApiSettings.RefreshToken`.
- On first token request it uses `BcsInvestApiSettings.RefreshToken`.
- After successful authorization it updates the in-memory token pair.
- Next refresh uses the in-memory rotated `refresh_token` while it is non-empty and valid under `TokenRefreshSkew`.
- If the in-memory refresh token is missing or expires within `TokenRefreshSkew`, refresh falls back to `BcsInvestApiSettings.RefreshToken`.
- If the in-memory refresh token is rejected with `invalid_grant`, refresh clears the in-memory token pair and retries
  once with `BcsInvestApiSettings.RefreshToken` when it is a different token.
- If `BcsInvestApiSettings.RefreshToken` is rejected with `invalid_grant`, the `BcsAuthException` is propagated.
- `GetAccessTokenAsync()` returns the in-memory access token while it is valid under `TokenRefreshSkew`.
- If the access token expires soon, it calls the auth endpoint under a `SemaphoreSlim` refresh gate.
- Concurrent callers re-check the in-memory token after entering the refresh gate.
- `RefreshAsync()` always calls the auth endpoint.

## Usage Examples

### Factory

```csharp
using Bcs.InvestApi;
using Bcs.InvestApi.Auth;

var refreshToken = Environment.GetEnvironmentVariable("BCS_REFRESH_TOKEN")
    ?? throw new InvalidOperationException("BCS_REFRESH_TOKEN is not set.");

await using var client = BcsInvestApiClientFactory.Create(
    refreshToken: refreshToken,
    clientId: BcsAuthClientIds.TradeApiRead);

var accessToken = await client.Tokens.GetAccessTokenAsync();
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

`AddBcsInvestApiClient` registers `BcsTokenManager` as a singleton and exposes it through `IBcsAccessTokenProvider`.

Inject the facade:

```csharp
public sealed class MyService
{
    private readonly BcsInvestApiClient _client;

    public MyService(BcsInvestApiClient client)
    {
        _client = client;
    }

    public Task<string> GetAccessTokenAsync(CancellationToken ct) =>
        _client.Tokens.GetAccessTokenAsync(ct).AsTask();
}
```

Or inject the token provider abstraction:

```csharp
using Bcs.InvestApi.Tokens;

public sealed class MyApiClient
{
    private readonly IBcsAccessTokenProvider _tokens;

    public MyApiClient(IBcsAccessTokenProvider tokens)
    {
        _tokens = tokens;
    }

    public async Task<HttpRequestMessage> CreateRequestAsync(CancellationToken ct)
    {
        var accessToken = await _tokens.GetAccessTokenAsync(ct);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test");
        request.Headers.Authorization = new("Bearer", accessToken);
        return request;
    }
}
```

## Auto-Refresh

Lazy refresh works without `StartAutoRefresh()`: `GetAccessTokenAsync()` refreshes the token when the current access
token is near expiration.

Use `StartAutoRefresh()` only when the host wants a background timer to check the current token before normal calls
arrive:

```csharp
client.Tokens.AutoRefreshFailed += (_, args) =>
{
    Console.Error.WriteLine(args.Exception.Message);
};

client.Tokens.StartAutoRefresh();

try
{
    var accessToken = await client.Tokens.GetAccessTokenAsync();
}
finally
{
    await client.Tokens.StopAutoRefreshAsync();
}
```

The auto-refresh loop uses the same lazy check and does not call the auth endpoint on every timer tick while the current
access token is still usable. If BCS rejects the in-memory refresh token with `invalid_grant`, the manager clears it and
tries the configured bootstrap refresh token once. If the bootstrap refresh token is rejected with `invalid_grant`, the
loop stops and the failure is available through `LastAutoRefreshException` and `AutoRefreshFailed`. Other refresh
failures are reported through the same APIs and the loop keeps running.

## Low-Level Raw Auth Usage

`BcsAuthService` is available when the caller wants only one raw token exchange:

```csharp
BcsAuthResponse response = await client.Auth.GetAccessTokenAsync(new BcsAuthRequest
{
    ClientId = BcsAuthClientIds.TradeApiWrite,
    RefreshToken = "<refresh-token>",
    GrantType = BcsGrantTypes.RefreshToken,
});
```

## HTTP Retries

The SDK keeps retry policy selection explicit:

- Auth refresh-token exchange uses a dedicated sender with retries disabled by default. Refresh tokens rotate on
  successful exchange, so retrying the same refresh token after a timeout, reset or gateway failure can turn a processed
  first request into a later `400 invalid_grant`.
- Read/query requests use the read sender. This covers GET endpoints such as limits, portfolio, instruments and
  candles, plus POST endpoints that are documented as idempotent read queries.
- Command requests use the command sender and are not retried by default.

Defaults:

- `AuthRetryAttempts = 0`
- `HttpRetryAttempts = 3`
- `HttpRetryBaseDelay = 250ms`
- read/query exponential delays: 250ms, 500ms, 1000ms

Keep `AuthRetryAttempts = 0` unless the caller has an external guarantee that retrying the refresh-token exchange is
safe. Command/order retries must be opt-in at the client operation level, not inherited from the shared HTTP layer.

## Error Handling

BCS `400 invalid_grant` response is exposed as `BcsAuthException`:

```csharp
try
{
    var token = await client.Tokens.GetTokenSetAsync();
}
catch (BcsAuthException ex) when (ex.Error == "invalid_grant")
{
    Console.WriteLine(ex.ErrorDescription);
}
```

## HttpClient Ownership

`BcsAuthService` does not implement `IDisposable` and does not dispose a `HttpClient` passed from outside.

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

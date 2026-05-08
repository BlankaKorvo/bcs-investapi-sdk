# Bcs.InvestApi

Thin C# SDK for BCS Trade API.

Iteration 2 contains authorization, access/refresh token storage and timer-based token refresh.

## Target framework

```text
net10.0
```

## Scope of iteration 2

Included:

- `BcsInvestApiClient` facade.
- `BcsAuthService` low-level authorization service.
- `BcsTokenManager` token manager.
- `IBcsAccessTokenProvider` abstraction for future HTTP API clients.
- In-memory token storage: `BcsInMemoryTokenStore`.
- JSON file token storage: `BcsFileTokenStore`.
- Timer-based token refresh via `BcsTokenManager.StartAutoRefresh()`.
- Polly-based HTTP retries for transient request failures.
- Raw auth request/response DTOs.
- Typed `BcsAuthException` for non-success auth responses.
- Typed `BcsRefreshTokenExpiredException` for locally known expired stored refresh token.
- Unit tests for form POST, response parsing, `invalid_grant`, token rotation and token storage.

Not included:

- Instruments.
- Market data.
- Orders.
- Portfolio/limits.
- WebSocket.
- Domain mapping or canonical models.
- Encrypted token vault. File storage writes plaintext JSON and must be protected by OS file permissions.

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
| `refresh_token` | refresh token from BCS Investments web UI, then the latest rotated `refresh_token` from storage |
| `grant_type` | `refresh_token` |

Response fields supported by DTO:

| JSON field | C# property |
|---|---|
| `access_token` | `BcsAuthResponse.AccessToken` |
| `expires_in` | `BcsAuthResponse.ExpiresIn` |
| `refresh_expires_in` | `BcsAuthResponse.RefreshExpiresIn` |
| `refresh_token` | `BcsAuthResponse.RefreshToken` |
| `token_type` | `BcsAuthResponse.TokenType` |
| `not-before-policy` | `BcsAuthResponse.NotBeforePolicy` |
| `session_state` | `BcsAuthResponse.SessionState` |
| `scope` | `BcsAuthResponse.Scope` |

## Token manager behavior

`BcsTokenManager` keeps the raw token pair as infrastructure state:

- On first call it uses `BcsInvestApiSettings.RefreshToken` if storage is empty.
- After successful authorization it saves both `access_token` and the rotated `refresh_token`.
- Next refresh uses the stored rotated `refresh_token`, not the original token from settings.
- `GetAccessTokenAsync()` returns the stored access token while it is valid.
- If the access token expires soon, it calls the auth endpoint and stores a new pair.
- The default refresh skew is 5 minutes before `access_token` expiration.
- The timer tick interval is 1 minute by default.
- The timer does not call the auth endpoint every minute; it only checks whether refresh is required.

## Factory usage with in-memory storage

Use one `BcsTokenManager`/`BcsInvestApiClient` per refresh token when using the default in-memory storage. Each
`BcsInvestApiClientFactory.Create(...)` call without a supplied `IBcsTokenStore` creates an independent in-memory
store, so several factory-created clients with the same initial refresh token can race each other during refresh token
rotation.

```csharp
using Bcs.InvestApi;
using Bcs.InvestApi.Auth;

await using var client = BcsInvestApiClientFactory.Create(
    refreshToken: "<initial-refresh-token>",
    clientId: BcsAuthClientIds.TradeApiRead);

string accessToken = await client.Tokens.GetAccessTokenAsync();
```

For multiple direct clients/managers in one process, share the same `BcsInMemoryTokenStore`. The store also implements
`IBcsTokenRefreshCoordinator`, and `BcsTokenManager` uses it automatically to serialize refresh operations:

```csharp
using Bcs.InvestApi;
using Bcs.InvestApi.Auth;
using Bcs.InvestApi.Tokens;

var settings = new BcsInvestApiSettings
{
    RefreshToken = "<initial-refresh-token>",
    ClientId = BcsAuthClientIds.TradeApiRead,
};

var tokenStore = new BcsInMemoryTokenStore();

await using var client1 = BcsInvestApiClientFactory.Create(settings, tokenStore: tokenStore);
await using var client2 = BcsInvestApiClientFactory.Create(settings, tokenStore: tokenStore);
```

## Factory usage with file storage

```csharp
using Bcs.InvestApi;
using Bcs.InvestApi.Auth;

await using var client = BcsInvestApiClientFactory.Create(new BcsInvestApiSettings
{
    RefreshToken = "<initial-refresh-token>",
    ClientId = BcsAuthClientIds.TradeApiRead,
    TokenStoragePath = @"C:\secure\bcs\tokens.json",
    TokenRefreshSkew = TimeSpan.FromMinutes(5),
    AutoRefreshInterval = TimeSpan.FromMinutes(1),
});

BcsTokenSet token = await client.Tokens.GetTokenSetAsync();
client.Tokens.StartAutoRefresh();
```

The saved file contains token values and calculated expiration timestamps:

```json
{
  "access_token": "...",
  "refresh_token": "...",
  "token_type": "bearer",
  "expires_in": 86400,
  "refresh_expires_in": 7776000,
  "access_token_expires_at_utc": "2026-05-03T12:00:00+00:00",
  "refresh_token_expires_at_utc": "2026-07-31T12:00:00+00:00",
  "received_at_utc": "2026-05-02T12:00:00+00:00",
  "not-before-policy": "0",
  "session_state": "...",
  "scope": "trade-api-read"
}
```

## Low-level raw auth usage

`BcsAuthService` is still available when the caller wants only one raw token exchange:

```csharp
BcsAuthResponse response = await client.Auth.GetAccessTokenAsync(new BcsAuthRequest
{
    ClientId = BcsAuthClientIds.TradeApiWrite,
    RefreshToken = "<refresh-token>",
    GrantType = BcsGrantTypes.RefreshToken,
});
```

## DI usage

```csharp
using Bcs.InvestApi;
using Bcs.InvestApi.Auth;
using Bcs.InvestApi.Tokens;

services.AddBcsInvestApiClient(settings =>
{
    settings.RefreshToken = "<initial-refresh-token>";
    settings.ClientId = BcsAuthClientIds.TradeApiRead;
    settings.TokenStoragePath = @"C:\secure\bcs\tokens.json";
});
```

Then inject either the facade:

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

## HTTP retries

All SDK HTTP requests go through the shared Polly retry sender. It retries `HttpRequestException`, timeout exceptions,
HTTP `408`, HTTP `429`, and HTTP `5xx` responses. Client/auth errors such as `400 invalid_grant` are not retried.

Defaults:

- `HttpRetryAttempts = 3`
- `HttpRetryBaseDelay = 250ms`
- exponential delays: 250ms, 500ms, 1000ms

Set `HttpRetryAttempts = 0` to disable retries, or adjust `HttpRetryBaseDelay` in `BcsInvestApiSettings`.

## Error handling

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

A locally stored refresh token that is already past its saved expiration timestamp is exposed as `BcsRefreshTokenExpiredException`.

## HttpClient ownership

`BcsAuthService` no longer implements `IDisposable` and no longer disposes a `HttpClient` passed from outside.

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

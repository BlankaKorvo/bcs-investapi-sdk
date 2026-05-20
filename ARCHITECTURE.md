# Bcs.InvestApi Architecture

Bcs.InvestApi is a thin transport + DTO + auth helper SDK for BCS Trade API.

The SDK boundary is intentionally small:

- HTTP transport for BCS Trade API endpoints.
- Raw request/response DTOs that stay close to the JSON API.
- Endpoint methods that map directly to server endpoints.
- Minimal technical validation for null/empty arguments and unsafe transport settings.
- Auth helper code used only to authorize SDK HTTP requests.
- In-memory access/refresh token cache for the current process.

## Boundary Rules

- Application layer owns business logic.
- Raw endpoints stay in SDK, including portfolio, limits, instruments, market data, and trading schedule endpoints.
- Application layer owns trading workflows, portfolio aggregation, normalization, strategy models, and DTO-to-domain mapping.
- Application layer owns pagination loops. SDK endpoint methods perform one server request per call and do not auto-paginate.
- Application layer owns durable secret storage. SDK does not persist tokens to disk.
- No disk token persistence.
- No background token daemon or external token store in SDK core.
- No automatic refresh-token retry. The refresh-token grant is not wrapped in retry policies, Polly, backoff, or hidden resilience pipelines.
- No hidden smart wrappers over raw endpoints.

## Package Shape

`Bcs.InvestApi` is the core package. It must not depend on `Microsoft.Extensions.*`.

`Bcs.InvestApi.DependencyInjection` is a separate package for Microsoft.Extensions.DependencyInjection registration.

## Auth Scope

The SDK may hold the runtime access token and rotated refresh token in memory so it can authorize broker endpoint
requests. That token state is private SDK runtime state:

- It is lost when the process exits.
- It is not written to files, user profiles, databases, or other stores.
- It is not exposed through the public facade.

The host/application layer supplies the stable bootstrap refresh token or equivalent secret when constructing the SDK.
If an application needs durable secret storage, rotation policy, retry policy, audit logging, or operational recovery, that
belongs above the SDK.

## Endpoint Scope

Endpoint methods remain in the SDK when they are direct server endpoints. Portfolio is a raw server endpoint and stays in
the SDK. The SDK should not remove raw endpoints because an application could build higher-level behavior on top of them.

The SDK should not add application-level helpers such as:

- strategy/domain models;
- repository/store abstractions;
- portfolio aggregation;
- DTO-to-domain mapping;
- backtest or trading-system workflows;
- pagination loops that call multiple pages automatically.

## Scope Matrix

Use this table when deciding whether a change belongs in the SDK or in the application layer.

| In SDK | In application |
|---|---|
| JSON field shape (snake-case, `[JsonPropertyName]`, hyphenated keys) | Server numeric limits enforced upfront (e.g. `size <= 100`, candles `bars <= 1440`, BCS-specific business ceilings) |
| Required fields and "required if" structural rules from docs (e.g. `baseAssetTicker` required when `type=OPTIONS`) | Domain validation that depends on business state (whitelists of tickers, account-level ceilings, daily quotas) |
| Format-level validation (`Guid.Empty` rejected, `string?` non-empty, ISO date formatting) | Numeric range / size validation (page sizes, quantities, price floors) |
| Serializing optional body fields as omitted (`[JsonIgnore(WhenWritingNull)]`) when docs mark them optional | When to send `null` vs. an explicit default value |
| Parsing the common error envelope into `BcsApiException.ApiError` (timestamp/traceId/type/errors/displayOptions) | Reaction to an error (retry, backoff, alerting, user-visible message) |
| Identifying server error codes as constants (`BcsApiErrorTypes.ResourceExhausted`, `…ValidationError`, etc.) | Interpreting what to do when each code arrives, and any endpoint-specific codes (e.g. `CANDLE_LIMIT_EXCEEDED`) the application wants to match |
| Auth: refresh-token grant, in-memory access-token cache, `BcsTokenManager` lifecycle | Durable secret storage, key rotation policy, audit logging, alerts on auth failure |
| URL and header construction, JSON options (`BcsJson.SerializerOptions`) | Persistent configuration files, environment binding, deployment topology |
| Opening an authenticated `ClientWebSocket` (handshake + `Authorization: Bearer`) | Long-lived WebSocket lifecycle: reconnect policy, backoff, ping/pong, multiplexing, buffering, dispatch to consumers |
| Static URL constants for known endpoints (HTTP and WebSocket) | Overriding URLs for tests, staging environments, alternative regions |
| Server-defined enum values surfaced as constants or typed enums | Translating those enums into domain language for the UI/business code |

When in doubt:

- If a check is rooted in the JSON contract or the auth/transport protocol, it belongs in the SDK.
- If a check is rooted in a tunable numeric limit, a policy, or a runtime decision, it belongs in the application.
- Server limits are last-resort enforcement. The SDK relays the error; the application owns the response.

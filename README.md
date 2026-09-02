# Bcs.InvestApi

Компактный C# SDK для BCS Trade API.

Bcs.InvestApi — это клиент прямых конечных точек: только транспорт, контрактные типы и вспомогательное средство авторизации.

SDK не выполняет агрегацию, нормализацию, маппинг, пагинацию, торговлю, планирование или интерпретацию данных. Это не слой приложения, не демон токенов, не торговая система, не модель портфеля и не стратегический фреймворк.

См. [ARCHITECTURE.md](ARCHITECTURE.md) для правил границ, которые этот пакет должен соблюдать.

## Целевая платформа

```text
net10.0
```

## Структура пакета

- `Bcs.InvestApi` содержит ядро SDK: вспомогательное средство авторизации, кэш токенов в памяти, HTTP-транспорт, контрактные DTO и методы конечных точек.
- `Bcs.InvestApi.DependencyInjection` содержит необязательную регистрацию Microsoft.Extensions.DependencyInjection.
- Ядро SDK не зависит от `Microsoft.Extensions.*`.

## Границы SDK

- Методы конечных точек выполняют один запрос к серверу BCS за вызов.
- Контрактные DTO прямых конечных точек остаются «сырыми». Бизнес-логику, маппинг домена, агрегацию, нормализацию, циклы пагинации, торговые решения, планирование, политику повторных попыток, постоянное хранение и операционное восстановление выполняет вызывающая сторона.
- Прямые конечные точки остаются в SDK, включая портфель, поскольку это прямые конечные точки сервера BCS, а не моделирование портфеля на уровне SDK.
- Постоянное хранение токенов на диске отсутствует.
- Автоматическая политика повторных попыток обновления токена при выдаче обновлённого токена отсутствует.
- Стратегические/доменные модели, репозитории, хранилища, бэктесты, Hosted Service и торговые системы отсутствуют.

## Возможности

- Авторизация по стабильному внешнему секрету обновления/загрузки, предоставленному хост-приложением.
- Кэш access/refresh токенов в памяти во время выполнения.
- Ленивое обновление access-токена до истечения срока его действия.
- Внутренний токен-менеджер, который авторизует HTTP-запросы SDK.
- Типизированное исключение `BcsAuthException` для неудачных ответов авторизации.
- Методы прямых конечных точек для лимитов, портфеля, торгового расписания, инструментов, исторических свечей, заявок, сделок и маржинальных дисконтов инструментов.

## Каталог конечных точек

Конечные точки сгруппированы по доменам на клиенте (например, `client.Orders.CreateOrderAsync(...)`).

| Метод | Конечная точка BCS |
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

## WebSocket-потоки

SDK предоставляет только инфраструкцию аутентифицированного WebSocket-соединения. Жизненный цикл соединения — переподключение, обратное давление, ping/pong, мультиплексирование и маршрутизация сообщений — принадлежит слою приложения (см. [ARCHITECTURE.md](ARCHITECTURE.md) — матрицу областей).

- `WebSockets.ConnectAsync(Uri url, CancellationToken)` открывает аутентифицированный `ClientWebSocket` (свежий Bearer-токен при каждом вызове).
- `WebSockets.ConnectAndSubscribeAsync(url, subscribeMessage, ct)` открывает сокет и отправляет содержимое подписки в первом кадре (для потоков, требующих явной подписки, например рыночные данные).
- `WebSocket.SendJsonMessageAsync(message, ct)` сериализует и отправляет один текстовый кадр с JSON-параметрами SDK (содержимое подписки/отписки).
- `WebSocket.ReceiveJsonMessageAsync<T>(ct)` считывает одно логическое сообщение; `WebSocket.ReceiveJsonMessagesAsync<T>(ct)` передаёт их как `IAsyncEnumerable<T>` до чистого закрытия или отмены.
- `BcsWebSocketEndpoints` предоставляет известные URL потоков (например, `BcsWebSocketEndpoints.Limits`).
- Полезные данные потоков используют контрактные типы HTTP (например, поток лимитов возвращает `BcsLimitsResponse`).

Пуш-поток (сообщение подписки не требуется):

```csharp
using var ws = await client.WebSockets.ConnectAsync(BcsWebSocketEndpoints.Limits, ct);
await foreach (var snapshot in ws.ReceiveJsonMessagesAsync<BcsLimitsResponse>(ct))
{
    Handle(snapshot); // перечисление завершается при чистом закрытии или отмене
}
```

Поток рыночных данных (подключение + подписка, затем отписка):

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

## Конечная точка авторизации

```text
POST https://be.broker.ru/trade-api-keycloak/realms/tradeapi/protocol/openid-connect/token
Content-Type: application/x-www-form-urlencoded
```

`BcsInvestApiSettings.AuthUrl` должен быть абсолютным HTTPS-URI. Простой HTTP по умолчанию отвергается, поскольку SDK отправляет refresh-токены на эту конечную точку. Для локальных тестов по HTTP явно задайте `AllowInsecureHttpForTesting = true`.

API- endpoints формируются относительно `BcsInvestApiSettings.BaseUrl`, которая по умолчанию равна `https://be.broker.ru`.

Поля формы:

| Поле | Значение |
|---|---|
| `client_id` | `trade-api-read` или `trade-api-write` |
| `refresh_token` | текущий в памяти вращающийся refresh-токен при доступности, иначе настроенный внешний секрет обновления/загрузки |
| `grant_type` | `refresh_token` |

## Настройки

| Настройка | По умолчанию | Описание |
|---|---|---|
| `RefreshToken` | требуется | Стабильный внешний секрет обновления/загрузки, предоставленный хостом/слоем приложения. |
| `ClientId` | `BcsAuthClientIds.TradeApiRead` | Перечисление идентификатора клиента BCS. Значения соответствуют `trade-api-read` или `trade-api-write`. |
| `AuthUrl` | Конечная точка токена BCS | Полный URL конечной точки Keycloak. Должен быть абсолютным HTTPS, если локальный небезопасный HTTP не разрешён явно. |
| `BaseUrl` | `https://be.broker.ru` | Базовый URL для API-ендпоинтов BCS. Должен быть абсолютным HTTPS, если локальный небезопасный HTTP не разрешён явно. |
| `AllowInsecureHttpForTesting` | `false` | Разрешает небезопасные HTTP-URL только для явных локальных тестов. |
| `Timeout` | `null` | Необязательный HTTP-таймаут. Если `null`, используется таймаут по умолчанию `HttpClient`. |
| `TokenRefreshSkew` | `5 minutes` | Обновление access-токена до его фактического истечения. |
| `TokenRefreshOperationTimeout` | `60 seconds` | Максимальное время, допустимое для одного обмена авторизации refresh-токеном. |

## Поведение авторизации

SDK хранит пару runtime-токенов в частном состоянии в памяти. Хост предоставляет один стабильный внешний секрет обновления/загрузки через `BcsInvestApiSettings.RefreshToken` или `BcsInvestApiClientFactory.Create(...)`.

Перед каждым запросом к брокерскому API SDK внутренне получает рабочий access-токен. Первый авторизованный запрос использует настроенный внешний секрет. Успешная авторизация обновляет пару токенов в памяти. Последующие вызовы обновления используют вращающийся `refresh_token` в памяти, пока он доступен; если он отсутствует или истекает в течение `TokenRefreshSkew`, SDK повторно использует настроенный внешний секрет.

Сбои выдачи refresh-токена распространяются как `BcsAuthException`. SDK не применяет политики повторных попыток, Polly, обратного давления, постоянного хранения токенов на диске или скрытых резилиент-обёрток вокруг выдачи. Если процесс завершается, состояние токенов в памяти теряется.

## Прямое использование

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

Приведённые ниже вызовы являются независимыми примерами прямых конечных точек, а не оркестрационным потоком:

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

## Использование с DI

Устанавливайте/ссылайтесь на `Bcs.InvestApi.DependencyInjection` только когда хост-приложение уже использует Microsoft.Extensions.DependencyInjection.

```csharp
using Bcs.InvestApi;
using Bcs.InvestApi.Contracts.Enums;

services.AddBcsInvestApiClient(settings =>
{
    settings.RefreshToken = configuration["BCS_REFRESH_TOKEN"];
    settings.ClientId = BcsAuthClientIds.TradeApiRead;
});
```

`AddBcsInvestApiClient` регистрирует фасад и внутренние токен-сервисы. Использование конечных точек такое же, как показано выше.

## Пагинация

SDK запрашивает ровно одну страницу. Методы поиска инструментов, заявок и сделок выполняют один запрос к BCS за вызов; вызывающая сторона явно передаёт `page` и `size` и самостоятельно ведёт любую политику многостраничной итерации. Сервер отвечает за валидацию поддерживаемых значений и максимальных лимитов.

```csharp
var instruments = await client.Instruments.GetInstrumentsByIsinsAsync(
    new[] { "RU0009029540", "RU0007661625", "RU000A0J2Q06" },
    page: 0,
    size: 50);
```

Если для конечной точки BCS нужен базовый тикер актива, передайте `baseAssetTicker`; SDK передаёт его как query-параметр при наличии.

```csharp
var sberOptions = await client.Instruments.GetInstrumentsByTypeAsync(
    BcsInstrumentTypes.Options,
    page: 0,
    size: 50,
    baseAssetTicker: "SBER");
```

## Исторические свечи

`MarketData.GetCandlesAsync(...)` вызывает `GET /trade-api-market-data-connector/api/v1/candles-chart`.

```csharp
using Bcs.InvestApi.Contracts.Enums;

var candles = await client.MarketData.GetCandlesAsync(
    classCode: "TQBR",
    ticker: "SBER",
    startDate: new DateTimeOffset(2025, 11, 14, 7, 0, 0, TimeSpan.Zero),
    endDate: new DateTimeOffset(2025, 11, 14, 10, 0, 0, TimeSpan.Zero),
    timeFrame: BcsCandleTimeFrames.Hour1);
```

BCS разрешает не более 1440 свечей в одном запросе.

## Пример

`sample/Bcs.InvestApi.Sample` — это командная консоль для вызовов прямых конечных точек. Это не рекомендуемая архитектура приложения и намеренно не демонстрирует оркестрацию, хостинг, логирование, повторные попытки, фоновые сервисы или доменные рабочие процессы.

Показать доступные команды:

```bash
dotnet run --project sample/Bcs.InvestApi.Sample -- help
```

Выполните одну команду с первым аргументом командной строки, `BCS_SAMPLE_COMMAND` или устаревшим `BCS_SAMPLE_ENDPOINT`:

```bash
dotnet run --project sample/Bcs.InvestApi.Sample -- limits
dotnet run --project sample/Bcs.InvestApi.Sample -- candles
dotnet run --project sample/Bcs.InvestApi.Sample -- stream-quotes
```

Поддерживаемые команды чтения:

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

Поддерживаемые WebSocket-команды:

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

Торговые команды явные и по умолчанию используют `trade-api-write`:

```text
order-create
order-update
order-cancel
```

Для всех команд, кроме `help`, образец требует `BCS_REFRESH_TOKEN` и не содержит реальных токенов.

Общие переменные среды:

| Переменная | По умолчанию | Используется |
|---|---|---|
| `BCS_SAMPLE_CLIENT_ID` | `read` или `write` для торговых команд | все команды |
| `BCS_SAMPLE_CLASS_CODE` | `TQBR` | инструменты, рыночные данные, расписание, заявки, сделки, потоки |
| `BCS_SAMPLE_TICKER` | `SBER` | инструменты, рыночные данные, расписание, заявки, сделки, потоки |
| `BCS_SAMPLE_ISINS` | `RU0009029540,RU0007661625,RU000A0J2Q06` | `instruments-by-isin` |
| `BCS_SAMPLE_TICKERS` | `SBER,GAZP,ROSN` | `instruments-by-ticker` |
| `BCS_SAMPLE_PAGE` | `0` | HTTP-команды с пагинацией |
| `BCS_SAMPLE_SIZE` | `10` | HTTP-команды с пагинацией |
| `BCS_SAMPLE_SEARCH_FROM` | UTC сейчас минус 7 дней | `orders`, `trades` |
| `BCS_SAMPLE_SEARCH_TO` | UTC сейчас | `orders`, `trades` |
| `BCS_SAMPLE_CANDLE_TIMEFRAME` | `Minute1` | `candles`, `stream-candles` |
| `BCS_SAMPLE_CANDLES_FROM` | UTC сейчас минус 1 день | `candles` |
| `BCS_SAMPLE_CANDLES_TO` | UTC сейчас | `candles` |
| `BCS_SAMPLE_INSTRUMENT_TYPE` | `Stock` | `instruments-by-type` |
| `BCS_SAMPLE_BASE_ASSET_TICKER` | не задано | `instruments-by-type` |
| `BCS_SAMPLE_STREAM_MESSAGES` | `5` | команды потоков |
| `BCS_SAMPLE_STREAM_TIMEOUT_SECONDS` | `30` | команды потоков |
| `BCS_SAMPLE_ORDERBOOK_DEPTH` | `20` | `stream-orderbook` |

Переменные, специфичные для заявок:

| Операция | Обязательно | Необязательно |
|---|---|---|
| Статус | `BCS_SAMPLE_ORIGINAL_CLIENT_ORDER_ID` | устаревший псевдоним: `BCS_SAMPLE_ORDER_STATUS_CLIENT_ORDER_ID` |
| Создать | `BCS_SAMPLE_ORDER_QUANTITY`; `BCS_SAMPLE_ORDER_PRICE` для нерыночных заявок | `BCS_SAMPLE_CLIENT_ORDER_ID`, `BCS_SAMPLE_ORDER_SIDE` (по умолчанию `Buy`), `BCS_SAMPLE_ORDER_TYPE` (по умолчанию `Limit`) |
| Обновить | `BCS_SAMPLE_ORIGINAL_CLIENT_ORDER_ID`, `BCS_SAMPLE_ORDER_QUANTITY` | `BCS_SAMPLE_CLIENT_ORDER_ID`, `BCS_SAMPLE_ORDER_PRICE` |
| Отменить | `BCS_SAMPLE_ORIGINAL_CLIENT_ORDER_ID` | `BCS_SAMPLE_CLIENT_ORDER_ID` |

Устаревшие псевдонимы переменных create/update/cancel из старых версий образца по-прежнему принимаются в доступных случаях (`BCS_SAMPLE_CREATE_*`, `BCS_SAMPLE_UPDATE_*`, `BCS_SAMPLE_CANCEL_*`). Некорректные значения переменных окружения быстро завершаются с ненулевым кодом до любого сетевого вызова.

## Граница «сырой» авторизации

`BcsInvestApiClient` не выставляет APIs сырой авторизационной обмена, API токен-менеджера, refresh-токены или управление жизненным циклом access-токена. Вращающиеся refresh-токены, возвращаемые BCS, остаются внутренней деталью SDK. Вызывающая сторона должна использовать брокерские API-методы, такие как `GetLimitsAsync(...)`, `GetPortfolioAsync(...)`, `GetDailyTradingScheduleAsync(...)`, `GetTradingScheduleStatusAsync(...)`, `GetInstrumentsBy...Async(...)`, `GetCandlesAsync(...)`, `CreateOrderAsync(...)`, `GetOrderStatusAsync(...)`, `SearchOrdersAsync(...)`, `UpdateOrderAsync(...)`, `CancelOrderAsync(...)`, `SearchTradesAsync(...)` и `GetInstrumentsDiscountsAsync(...)`.

Если позже потребуется диагностический или низкоуровневый API «сырой» авторизации, отделите его от основного фасада и сделайте выставление refresh-токена явным в этом API.

## Обработка ошибок

Неудачные ответы авторизации BCS выставляются как `BcsAuthException`:

```csharp
using Bcs.InvestApi.Contracts.Exceptions;

try
{
    var limits = await client.Limits.GetLimitsAsync();
}
catch (BcsAuthException ex) when (ex.Error == "invalid_grant")
{
    // Определите поведение восстановления в слое хоста/приложения.
    throw;
}
```

## Владение HttpClient

Внутренний авторизационный транспорт не освобождает `HttpClient`, переданный извне.

- В режиме DI `IHttpClientFactory` владеет жизненным циклом транспорта.
- В режиме прямой фабрики `BcsInvestApiClientFactory` создаёт и освобождает собственный `HttpClient` при освобождении фасада.
- Если к фабрике передан пользовательский `HttpMessageHandler`, SDK не освобождает обработчик.

## Сборка

```bash
dotnet build Bcs.InvestApi.sln
```

## Тесты

```bash
dotnet test Bcs.InvestApi.sln
```
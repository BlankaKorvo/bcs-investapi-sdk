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
| `Orders.CreateOrderSyncAsync` | `POST /trade-api-bff-operations/api/v1/orders/sync` |
| `Orders.GetOrderStatusAsync` | `GET /trade-api-bff-operations/api/v1/orders?orderIdType=&orderId=` |
| `Orders.SearchOrdersAsync` | `POST /trade-api-bff-order-details/api/v1/orders/search` |
| `Trades.SearchTradesAsync` | `POST /trade-api-bff-trade-details/api/v1/trades/search` |
| `MarginalIndicators.GetInstrumentsDiscountsAsync` | `GET /trade-api-bff-marginal-indicators/api/v1/instruments-discounts` |

Устаревшие у BCS методы заявок (статус по пути `/orders/{originalClientOrderId}`, изменение и отмена по этому пути; часть BCS удалит 31.01.2027) в SDK не входят. Новые отмена и изменение заявки (`/orders/cancel`, `/orders/edit` и их синхронные варианты) не реализованы.

### Заявки

- `CreateOrderAsync` — асинхронное создание: ответ только подтверждает приём запроса (`clientOrderId`, `status`).
- `CreateOrderSyncAsync` — синхронное создание: BCS дожидается итога обработки и отвечает состоянием заявки в формате статуса (`BcsOrderStatusResponse`). Тело запроса то же, что у `CreateOrderAsync`.
- `GetOrderStatusAsync(orderIdType, orderId)` — статус заявки по виду идентификатора: `BcsOrderIdType.ClientOrderId` (`1`) — UUID, с которым заявка создана (не UUID и пустой UUID — `ArgumentException` до запроса); `BcsOrderIdType.ExchangeOrderId` (`2`) — биржевой номер `ГГММДД-КодКласса-НомерЗаявки`. `Data` — массив отчётов о заявке. Заявки нет — `BcsApiException` с кодом 404.
- Типы заявки у создания и у списка заявок закодированы по-разному, поэтому это разные перечисления: `BcsCreateOrderType` (создание: `Market` = `1`, `Limit` = `2`; коды `3`–`5` — алгоритмическая GTD, айсберг и условная — требуют полей, которых `BcsCreateOrderRequest` не содержит, и SDK их не отправляет) и `BcsSearchOrderType` (фильтр и записи `SearchOrdersAsync`: `1`–`7`, `10`, `11`).

## WebSocket-потоки

SDK предоставляет только инфраструкцию аутентифицированного WebSocket-соединения. Жизненный цикл соединения — переподключение, обратное давление, ping/pong, мультиплексирование и маршрутизация сообщений — принадлежит слою приложения (см. [ARCHITECTURE.md](ARCHITECTURE.md) — матрицу областей).

- `WebSockets.ConnectAsync(Uri url, CancellationToken)` открывает аутентифицированный `ClientWebSocket` (свежий Bearer-токен при каждом вызове).
- `WebSockets.ConnectAndSubscribeAsync(url, subscribeMessage, ct)` открывает сокет и отправляет содержимое подписки в первом кадре (для потоков, требующих явной подписки, например рыночные данные).
- `WebSocket.SendJsonMessageAsync(message, ct)` сериализует и отправляет один текстовый кадр с JSON-параметрами SDK (содержимое подписки/отписки).
- `WebSocket.ReceiveJsonMessageAsync<T>(ct)` считывает одно логическое сообщение; `WebSocket.ReceiveJsonMessagesAsync<T>(ct)` передаёт их как `IAsyncEnumerable<T>` до чистого закрытия или отмены — единственный способ читать поток сообщений.
- `BcsWebSocketEndpoints` предоставляет известные URL потоков: `Limits`, `Portfolio`, `MarketData`, `OrderEvents`, `MarginalIndicators`. Поток статусов заявок `OrderEvents` (`orders/events/ws`) — пуш без подписки, сообщение `BcsOrderEventMessage` с одним отчётом о заявке в `data`; устаревшие потоки `orders/execution/ws` и `orders/transaction/ws` в SDK не входят.
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
| `ClientId` | требуется, умолчания нет | Идентификатор клиента BCS: `BcsAuthClientIds.TradeApiRead` (`trade-api-read`) или `BcsAuthClientIds.TradeApiWrite` (`trade-api-write`). От него зависит, может ли клиент торговать, поэтому хост задаёт его явно; не задан — `InvalidOperationException` при создании клиента. У `BcsInvestApiClientFactory.Create(refreshToken, clientId, ...)` параметр тоже обязателен. |
| `AuthUrl` | Конечная точка токена BCS | Полный URL конечной точки Keycloak. Должен быть абсолютным HTTPS, если локальный небезопасный HTTP не разрешён явно. |
| `BaseUrl` | `https://be.broker.ru` | Базовый URL для API-ендпоинтов BCS. Должен быть абсолютным HTTPS, если локальный небезопасный HTTP не разрешён явно. |
| `AllowInsecureHttpForTesting` | `false` | Разрешает небезопасные HTTP-URL только для явных локальных тестов. |
| `Timeout` | `null` | Необязательный HTTP-таймаут. Если `null`, используется таймаут по умолчанию `HttpClient`. |
| `TokenRefreshSkew` | `5 minutes` | Обновление access-токена до его фактического истечения. |
| `TokenRefreshOperationTimeout` | `60 seconds` | Максимальное время, допустимое для одного обмена авторизации refresh-токеном. |

## Поведение авторизации

SDK хранит пару runtime-токенов в частном состоянии в памяти. Хост предоставляет один стабильный внешний секрет обновления/загрузки через `BcsInvestApiSettings.RefreshToken` или `BcsInvestApiClientFactory.Create(...)`.

Перед каждым запросом к брокерскому API SDK внутренне получает рабочий access-токен. Первый авторизованный запрос использует настроенный внешний секрет. Успешная авторизация обновляет пару токенов в памяти. Последующие вызовы обновления используют вращающийся `refresh_token` в памяти, пока он доступен; если он отсутствует или истекает в течение `TokenRefreshSkew`, SDK повторно использует настроенный внешний секрет.

Любой ответ 401 от брокерского API сбрасывает access-токен из кэша до того, как исключение уйдёт вызывающему: следующий вызов получит новый токен (повторять вызов или нет — решает вызывающий). Заголовка `WWW-Authenticate` SDK не ждёт — документация BCS его не описывает.

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
var orderStatus = await client.Orders.GetOrderStatusAsync(
    BcsOrderIdType.ClientOrderId,
    "12345678-1234-1234-9adb-123456789876");
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

var order = new BcsCreateOrderRequest
{
    ClientOrderId = Guid.NewGuid(),
    Side = BcsOrderSide.Buy,
    OrderType = BcsCreateOrderType.Limit,
    OrderQuantity = 10,
    Ticker = "SBER",
    ClassCode = "TQBR",
    Price = 250.10m,
};

var orderState = await writeClient.Orders.CreateOrderSyncAsync(order); // итог обработки, формат статуса
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

`sample/Bcs.InvestApi.Sample` — консольный пример вызовов прямых конечных точек. Это не рекомендуемая архитектура приложения и намеренно не демонстрирует оркестрацию, хостинг, логирование, повторные попытки, фоновые сервисы или доменные рабочие процессы.

Пример по очереди вызывает HTTP-методы чтения (лимиты, портфель, инструменты, свечи, расписание, заявки, сделки, дисконты), затем — включённые переменными окружения статус заявки, создание заявки и потоки. Запуск:

```bash
dotnet run --project sample/Bcs.InvestApi.Sample
```

Пример требует `BCS_REFRESH_TOKEN` и не содержит реальных токенов. Некорректные значения переменных окружения завершают пример с ненулевым кодом до любого сетевого вызова.

| Переменная | По умолчанию | Используется |
|---|---|---|
| `BCS_SAMPLE_CLIENT_ID` | `write`, если включено создание заявки, иначе `read` | клиент (`read`, `write`, `trade-api-read`, `trade-api-write`) |
| `BCS_SAMPLE_CLASS_CODE` | `TQBR` | свечи, расписание, заявки, потоки |
| `BCS_SAMPLE_TICKER` | `SBER` | свечи, расписание, заявки, потоки |
| `BCS_SAMPLE_ISINS` | `RU0009029540,RU0007661625,RU000A0J2Q06` | инструменты по ISIN |
| `BCS_SAMPLE_TICKERS` | `SBER,GAZP,ROSN` | инструменты по тикерам |
| `BCS_SAMPLE_ORDER_STATUS_ID` | не задано — статус пропускается | статус заявки |
| `BCS_SAMPLE_ORDER_STATUS_ID_TYPE` | `ClientOrderId` | статус заявки: `ClientOrderId` (UUID) или `ExchangeOrderId` (биржевой номер) |
| `BCS_SAMPLE_CREATE_ORDER` | `false` | создание заявки |
| `BCS_SAMPLE_CREATE_ORDER_SYNC` | `false` | синхронное создание (`CreateOrderSyncAsync`) вместо асинхронного |
| `BCS_SAMPLE_CREATE_ORDER_QUANTITY` | требуется при создании | количество (шт.) |
| `BCS_SAMPLE_CREATE_ORDER_TYPE` | `Limit` | `Market` или `Limit` |
| `BCS_SAMPLE_CREATE_PRICE` | требуется для `Limit` | цена лимитной заявки |
| `BCS_SAMPLE_CREATE_SIDE` | `Buy` | `Buy` или `Sell` |
| `BCS_SAMPLE_CREATE_CLIENT_ORDER_ID` | новый UUID | `clientOrderId` заявки |

Потоки включаются по одному флагом `=true` и читают несколько сообщений: `BCS_SAMPLE_STREAM_CANDLES`, `BCS_SAMPLE_STREAM_ORDERBOOK`, `BCS_SAMPLE_STREAM_QUOTES`, `BCS_SAMPLE_STREAM_TRADES`, `BCS_SAMPLE_STREAM_LIMITS`, `BCS_SAMPLE_STREAM_PORTFOLIO`, `BCS_SAMPLE_STREAM_ORDER_EVENTS`, `BCS_SAMPLE_STREAM_MARGIN`.

## Граница «сырой» авторизации

`BcsInvestApiClient` не выставляет APIs сырой авторизационной обмена, API токен-менеджера, refresh-токены или управление жизненным циклом access-токена. Вращающиеся refresh-токены, возвращаемые BCS, остаются внутренней деталью SDK. Вызывающая сторона должна использовать брокерские API-методы, такие как `GetLimitsAsync(...)`, `GetPortfolioAsync(...)`, `GetDailyTradingScheduleAsync(...)`, `GetTradingScheduleStatusAsync(...)`, `GetInstrumentsBy...Async(...)`, `GetCandlesAsync(...)`, `CreateOrderAsync(...)`, `CreateOrderSyncAsync(...)`, `GetOrderStatusAsync(...)`, `SearchOrdersAsync(...)`, `SearchTradesAsync(...)` и `GetInstrumentsDiscountsAsync(...)`.

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
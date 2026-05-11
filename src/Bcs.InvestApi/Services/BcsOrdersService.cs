namespace Bcs.InvestApi.Services;

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bcs.InvestApi;
using Bcs.InvestApi.Contracts.Enums;
using Bcs.InvestApi.Contracts.Orders;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Tokens;

internal sealed class BcsOrdersService
{
    private readonly BcsApiRequestExecutor _executor;
    private readonly Uri _ordersCreateUrl;
    private readonly Uri _orderOperationsRootUrl;
    private readonly Uri _ordersSearchUrl;

    internal BcsOrdersService(
        BcsInvestApiSettings settings,
        HttpClient httpClient,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
        : this(settings, new BcsApiRequestExecutor(httpClient, tokens, requestSender))
    {
    }

    internal BcsOrdersService(
        BcsInvestApiSettings settings,
        Func<HttpClient> httpClientFactory,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
        : this(settings, new BcsApiRequestExecutor(httpClientFactory, tokens, requestSender))
    {
    }

    private BcsOrdersService(
        BcsInvestApiSettings settings,
        BcsApiRequestExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.ValidateBaseUrl();

        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _ordersCreateUrl = settings.CreateEndpointUrl(BcsEndpointPaths.Orders.Create);
        _ordersSearchUrl = settings.CreateEndpointUrl(BcsEndpointPaths.Orders.Search);
        _orderOperationsRootUrl = settings.CreateEndpointUrl(BcsEndpointPaths.Orders.OperationsRoot);
    }

    internal Task<BcsCreateOrderResponse> CreateOrderAsync(
        BcsCreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequiredGuid(request.ClientOrderId, nameof(request.ClientOrderId));
        var side = request.Side.ToApiValue().ToString(CultureInfo.InvariantCulture);
        var orderType = request.OrderType.ToApiValue().ToString(CultureInfo.InvariantCulture);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Ticker);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ClassCode);

        var requestBody = new BcsCreateOrderRequestBody(
            request.ClientOrderId,
            side,
            orderType,
            request.OrderQuantity,
            request.Ticker,
            request.ClassCode,
            request.Price);

        return _executor.SendJsonAsync<BcsCreateOrderResponse>(
            accessToken => CreateCreateOrderRequestMessage(accessToken, requestBody),
            "order-create",
            cancellationToken);
    }

    internal Task<BcsOrderStatusResponse> GetOrderStatusAsync(
        Guid clientOrderId,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredGuid(clientOrderId, nameof(clientOrderId));

        return _executor.SendJsonAsync<BcsOrderStatusResponse>(
            accessToken => CreateGetOrderStatusRequestMessage(accessToken, clientOrderId),
            "order-status",
            cancellationToken);
    }

    internal Task<BcsOrdersSearchResponse> SearchOrdersAsync(
        BcsOrdersSearchRequest request,
        int page,
        int size,
        IEnumerable<BcsOrderSort>? sort = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePage(page);
        ValidateSize(size);

        var sortValues = sort?.Select(value => value.ToApiValue()).ToArray();
        var requestBody = CreateRequestBody(request);

        return _executor.SendJsonAsync<BcsOrdersSearchResponse>(
            accessToken => CreateRequestMessage(accessToken, requestBody, page, size, sortValues),
            "orders-search",
            cancellationToken);
    }

    internal Task<BcsCancelOrderResponse> CancelOrderAsync(
        Guid originalClientOrderId,
        Guid clientOrderId,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredGuid(originalClientOrderId, nameof(originalClientOrderId));
        ValidateRequiredGuid(clientOrderId, nameof(clientOrderId));

        return _executor.SendJsonAsync<BcsCancelOrderResponse>(
            accessToken => CreateCancelOrderRequestMessage(accessToken, originalClientOrderId, clientOrderId),
            "order-cancel",
            cancellationToken);
    }

    internal Task<BcsUpdateOrderResponse> UpdateOrderAsync(
        Guid originalClientOrderId,
        BcsUpdateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredGuid(originalClientOrderId, nameof(originalClientOrderId));
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequiredGuid(request.ClientOrderId, nameof(request.ClientOrderId));

        var requestBody = new BcsUpdateOrderRequestBody(
            request.ClientOrderId,
            request.Price,
            request.OrderQuantity);

        return _executor.SendJsonAsync<BcsUpdateOrderResponse>(
            accessToken => CreateUpdateOrderRequestMessage(accessToken, originalClientOrderId, requestBody),
            "order-update",
            cancellationToken);
    }

    private static BcsOrdersSearchRequestBody CreateRequestBody(BcsOrdersSearchRequest request)
    {
        return new BcsOrdersSearchRequestBody
        {
            StartDateTime = request.StartDateTime is { } startDateTime
                ? FormatBodyDateTime(startDateTime)
                : null,
            EndDateTime = request.EndDateTime is { } endDateTime
                ? FormatBodyDateTime(endDateTime)
                : null,
            Side = request.Side?.ToApiValue(),
            OrderStatus = ToApiValues(request.OrderStatus),
            OrderTypes = ToApiValues(request.OrderTypes),
            Tickers = ValidateStringValues(request.Tickers, nameof(request.Tickers)),
            ClassCodes = ValidateStringValues(request.ClassCodes, nameof(request.ClassCodes)),
        };
    }

    private HttpRequestMessage CreateCreateOrderRequestMessage(
        string accessToken,
        BcsCreateOrderRequestBody requestBody)
    {
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, _ordersCreateUrl)
            .WithBearer(accessToken)
            .AcceptJson();

        requestMessage.Content = new StringContent(
            JsonSerializer.Serialize(requestBody, BcsJson.SerializerOptions),
            Encoding.UTF8,
            "application/json");

        return requestMessage;
    }

    private HttpRequestMessage CreateGetOrderStatusRequestMessage(
        string accessToken,
        Guid clientOrderId)
    {
        return new HttpRequestMessage(HttpMethod.Get, CreateGetOrderStatusUrl(clientOrderId))
            .WithBearer(accessToken)
            .AcceptJson();
    }

    private HttpRequestMessage CreateRequestMessage(
        string accessToken,
        BcsOrdersSearchRequestBody requestBody,
        int page,
        int size,
        IReadOnlyList<string>? sort)
    {
        var requestMessage = new HttpRequestMessage(
                HttpMethod.Post,
                CreateOrdersSearchUrl(page, size, sort))
            .WithBearer(accessToken)
            .AcceptJson();

        requestMessage.Content = new StringContent(
            JsonSerializer.Serialize(requestBody, BcsJson.SerializerOptions),
            Encoding.UTF8,
            "application/json");

        return requestMessage;
    }

    private Uri CreateOrdersSearchUrl(
        int page,
        int size,
        IReadOnlyList<string>? sort)
    {
        var query = string.Create(
            CultureInfo.InvariantCulture,
            $"page={page}&size={size}");

        if (sort is not null)
        {
            foreach (var value in sort)
            {
                query += $"&sort={Uri.EscapeDataString(value)}";
            }
        }

        var builder = new UriBuilder(_ordersSearchUrl)
        {
            Query = query,
        };

        return builder.Uri;
    }

    private HttpRequestMessage CreateCancelOrderRequestMessage(
        string accessToken,
        Guid originalClientOrderId,
        Guid clientOrderId)
    {
        var requestMessage = new HttpRequestMessage(
                HttpMethod.Post,
                CreateCancelOrderUrl(originalClientOrderId))
            .WithBearer(accessToken)
            .AcceptJson();

        requestMessage.Content = new StringContent(
            JsonSerializer.Serialize(new BcsCancelOrderRequestBody(clientOrderId), BcsJson.SerializerOptions),
            Encoding.UTF8,
            "application/json");

        return requestMessage;
    }

    private Uri CreateCancelOrderUrl(Guid originalClientOrderId)
    {
        var originalClientOrderIdValue = Uri.EscapeDataString(originalClientOrderId.ToString("D"));
        return new Uri(EnsureTrailingSlash(_orderOperationsRootUrl), $"{originalClientOrderIdValue}/cancel");
    }

    private HttpRequestMessage CreateUpdateOrderRequestMessage(
        string accessToken,
        Guid originalClientOrderId,
        BcsUpdateOrderRequestBody requestBody)
    {
        var requestMessage = new HttpRequestMessage(
                HttpMethod.Post,
                CreateUpdateOrderUrl(originalClientOrderId))
            .WithBearer(accessToken)
            .AcceptJson();

        requestMessage.Content = new StringContent(
            JsonSerializer.Serialize(requestBody, BcsJson.SerializerOptions),
            Encoding.UTF8,
            "application/json");

        return requestMessage;
    }

    private Uri CreateUpdateOrderUrl(Guid originalClientOrderId)
    {
        var originalClientOrderIdValue = Uri.EscapeDataString(originalClientOrderId.ToString("D"));
        return new Uri(EnsureTrailingSlash(_orderOperationsRootUrl), originalClientOrderIdValue);
    }

    private Uri CreateGetOrderStatusUrl(Guid clientOrderId)
    {
        var clientOrderIdValue = Uri.EscapeDataString(clientOrderId.ToString("D"));
        return new Uri(EnsureTrailingSlash(_orderOperationsRootUrl), clientOrderIdValue);
    }

    private static IReadOnlyList<int>? ToApiValues(IReadOnlyList<BcsOrderStatus>? values) =>
        values?.Select(value => value.ToApiValue()).ToArray();

    private static IReadOnlyList<int>? ToApiValues(IReadOnlyList<BcsOrderType>? values) =>
        values?.Select(value => value.ToApiValue()).ToArray();

    private static IReadOnlyList<string>? ValidateStringValues(
        IReadOnlyList<string>? values,
        string parameterName)
    {
        if (values is null)
        {
            return null;
        }

        if (values.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Filter values must not be null or whitespace.", parameterName);
        }

        return values;
    }

    private static void ValidatePage(int page)
    {
        if (page < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(page),
                page,
                "Page number must be greater than or equal to zero.");
        }
    }

    private static void ValidateSize(int size)
    {
        if (size < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(size),
                size,
                "Page size must be greater than or equal to one.");
        }
    }

    private static void ValidateRequiredGuid(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("BCS order UUID must not be empty.", parameterName);
        }
    }

    private static string FormatBodyDateTime(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        if (uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal))
        {
            return uri;
        }

        return new Uri(uri.AbsoluteUri + "/");
    }

    private sealed record BcsOrdersSearchRequestBody
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? StartDateTime { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? EndDateTime { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Side { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<int>? OrderStatus { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<int>? OrderTypes { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<string>? Tickers { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<string>? ClassCodes { get; init; }
    }

    private sealed record BcsCreateOrderRequestBody(
        Guid ClientOrderId,
        string Side,
        string OrderType,
        long OrderQuantity,
        string Ticker,
        string ClassCode,
        decimal Price);

    private sealed record BcsCancelOrderRequestBody(Guid ClientOrderId);

    private sealed record BcsUpdateOrderRequestBody(
        Guid ClientOrderId,
        decimal Price,
        long OrderQuantity);
}

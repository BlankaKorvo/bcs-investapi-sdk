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
        _ordersSearchUrl = settings.CreateEndpointUrl(BcsEndpointPaths.Orders.Search);
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

    private static string FormatBodyDateTime(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

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
}

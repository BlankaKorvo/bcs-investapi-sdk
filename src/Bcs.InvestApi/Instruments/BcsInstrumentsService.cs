namespace Bcs.InvestApi.Instruments;

using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Tokens;

internal sealed class BcsInstrumentsService
{
    private const string InstrumentsByIsinsPath = "trade-api-information-service/api/v1/instruments/by-isins";
    private const string InstrumentsByTickersPath = "trade-api-information-service/api/v1/instruments/by-tickers";
    private const string InstrumentsByTypePath = "trade-api-information-service/api/v1/instruments/by-type";
    private const int MaxPageSize = 100;

    private readonly Func<HttpClient> _httpClientFactory;
    private readonly bool _disposeHttpClientAfterRequest;
    private readonly Uri _instrumentsByIsinsUrl;
    private readonly Uri _instrumentsByTickersUrl;
    private readonly Uri _instrumentsByTypeUrl;
    private readonly IBcsHttpSender _requestSender;
    private readonly IBcsAccessTokenProvider _tokens;

    internal BcsInstrumentsService(
        BcsInvestApiSettings settings,
        HttpClient httpClient,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
        : this(settings, () => httpClient, tokens, requestSender, disposeHttpClientAfterRequest: false)
    {
    }

    internal BcsInstrumentsService(
        BcsInvestApiSettings settings,
        Func<HttpClient> httpClientFactory,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
        : this(settings, httpClientFactory, tokens, requestSender, disposeHttpClientAfterRequest: true)
    {
    }

    private BcsInstrumentsService(
        BcsInvestApiSettings settings,
        Func<HttpClient> httpClientFactory,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender,
        bool disposeHttpClientAfterRequest)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.ValidateTransportSettings();

        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _requestSender = requestSender ?? throw new ArgumentNullException(nameof(requestSender));
        _instrumentsByIsinsUrl = settings.CreateEndpointUrl(InstrumentsByIsinsPath);
        _instrumentsByTickersUrl = settings.CreateEndpointUrl(InstrumentsByTickersPath);
        _instrumentsByTypeUrl = settings.CreateEndpointUrl(InstrumentsByTypePath);
        _disposeHttpClientAfterRequest = disposeHttpClientAfterRequest;
    }

    internal async Task<IReadOnlyList<BcsInstrument>> GetInstrumentsByIsinsAsync(
        IEnumerable<string> isins,
        int page,
        int size,
        CancellationToken cancellationToken = default)
    {
        var requestedIsins = ValidateIsins(isins);
        ValidatePage(page);
        ValidateSize(size);

        var httpClient = _httpClientFactory();

        try
        {
            return await SendPageAsync(
                httpClient,
                requestedIsins,
                page,
                size,
                CreateIsinsRequestMessage,
                "instruments-by-isins",
                "BCS instruments by ISIN response body is empty or cannot be deserialized.",
                cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            if (_disposeHttpClientAfterRequest)
            {
                httpClient.Dispose();
            }
        }
    }

    internal async Task<IReadOnlyList<BcsInstrument>> GetInstrumentsByTickersAsync(
        IEnumerable<string> tickers,
        int page,
        int size,
        CancellationToken cancellationToken = default)
    {
        var requestedTickers = ValidateTickers(tickers);
        ValidatePage(page);
        ValidateSize(size);

        var httpClient = _httpClientFactory();

        try
        {
            return await SendPageAsync(
                httpClient,
                requestedTickers,
                page,
                size,
                CreateTickersRequestMessage,
                "instruments-by-tickers",
                "BCS instruments by ticker response body is empty or cannot be deserialized.",
                cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            if (_disposeHttpClientAfterRequest)
            {
                httpClient.Dispose();
            }
        }
    }

    internal async Task<IReadOnlyList<BcsInstrument>> GetInstrumentsByTypeAsync(
        string type,
        int page,
        int size,
        string? baseAssetTicker = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(type);
        ValidateBaseAssetTicker(type, baseAssetTicker);
        ValidatePage(page);
        ValidateSize(size);

        var httpClient = _httpClientFactory();

        try
        {
            return await SendTypePageAsync(
                httpClient,
                type,
                baseAssetTicker,
                page,
                size,
                cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            if (_disposeHttpClientAfterRequest)
            {
                httpClient.Dispose();
            }
        }
    }

    private async Task<IReadOnlyList<BcsInstrument>> SendPageAsync(
        HttpClient httpClient,
        IReadOnlyList<string> instrumentIds,
        int page,
        int size,
        Func<string, IReadOnlyList<string>, int, int, HttpRequestMessage> requestFactory,
        string endpoint,
        string emptyResponseBodyMessage,
        CancellationToken cancellationToken)
    {
        var responseBody = await BcsReadApiRequestExecutor
            .SendAsync(
                httpClient,
                _tokens,
                _requestSender,
                accessToken => requestFactory(accessToken, instrumentIds, page, size),
                endpoint,
                cancellationToken)
            .ConfigureAwait(false);

        var instruments = JsonSerializer.Deserialize<List<BcsInstrument>>(
            responseBody,
            BcsJson.SerializerOptions);

        if (instruments is null)
        {
            throw new JsonException(emptyResponseBodyMessage);
        }

        return instruments;
    }

    private async Task<IReadOnlyList<BcsInstrument>> SendTypePageAsync(
        HttpClient httpClient,
        string type,
        string? baseAssetTicker,
        int page,
        int size,
        CancellationToken cancellationToken)
    {
        var responseBody = await BcsReadApiRequestExecutor
            .SendAsync(
                httpClient,
                _tokens,
                _requestSender,
                accessToken => CreateTypeRequestMessage(accessToken, type, baseAssetTicker, page, size),
                "instruments-by-type",
                cancellationToken)
            .ConfigureAwait(false);

        var instruments = JsonSerializer.Deserialize<List<BcsInstrument>>(
            responseBody,
            BcsJson.SerializerOptions);

        if (instruments is null)
        {
            throw new JsonException("BCS instruments by type response body is empty or cannot be deserialized.");
        }

        return instruments;
    }

    private HttpRequestMessage CreateIsinsRequestMessage(
        string accessToken,
        IReadOnlyList<string> isins,
        int page,
        int size)
    {
        var requestMessage = new HttpRequestMessage(
            HttpMethod.Post,
            CreateInstrumentsByIsinsUrl(page, size));

        requestMessage.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        requestMessage.Content = new StringContent(
            JsonSerializer.Serialize(new BcsInstrumentsByIsinsRequest(isins), BcsJson.SerializerOptions),
            Encoding.UTF8,
            "application/json");

        return requestMessage;
    }

    private HttpRequestMessage CreateTypeRequestMessage(
        string accessToken,
        string type,
        string? baseAssetTicker,
        int page,
        int size)
    {
        var requestMessage = new HttpRequestMessage(
            HttpMethod.Get,
            CreateInstrumentsByTypeUrl(type, baseAssetTicker, page, size));

        requestMessage.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return requestMessage;
    }

    private HttpRequestMessage CreateTickersRequestMessage(
        string accessToken,
        IReadOnlyList<string> tickers,
        int page,
        int size)
    {
        var requestMessage = new HttpRequestMessage(
            HttpMethod.Post,
            CreateInstrumentsByTickersUrl(page, size));

        requestMessage.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        requestMessage.Content = new StringContent(
            JsonSerializer.Serialize(new BcsInstrumentsByTickersRequest(tickers), BcsJson.SerializerOptions),
            Encoding.UTF8,
            "application/json");

        return requestMessage;
    }

    private Uri CreateInstrumentsByIsinsUrl(int page, int size)
    {
        var query = string.Create(
            CultureInfo.InvariantCulture,
            $"size={size}&page={page}");

        var builder = new UriBuilder(_instrumentsByIsinsUrl)
        {
            Query = query,
        };

        return builder.Uri;
    }

    private Uri CreateInstrumentsByTypeUrl(
        string type,
        string? baseAssetTicker,
        int page,
        int size)
    {
        var query = string.Create(
            CultureInfo.InvariantCulture,
            $"type={Uri.EscapeDataString(type)}");

        if (!string.IsNullOrEmpty(baseAssetTicker))
        {
            query += string.Create(
                CultureInfo.InvariantCulture,
                $"&baseAssetTicker={Uri.EscapeDataString(baseAssetTicker)}");
        }

        query += string.Create(
            CultureInfo.InvariantCulture,
            $"&size={size}&page={page}");

        var builder = new UriBuilder(_instrumentsByTypeUrl)
        {
            Query = query,
        };

        return builder.Uri;
    }

    private Uri CreateInstrumentsByTickersUrl(int page, int size)
    {
        var query = string.Create(
            CultureInfo.InvariantCulture,
            $"size={size}&page={page}");

        var builder = new UriBuilder(_instrumentsByTickersUrl)
        {
            Query = query,
        };

        return builder.Uri;
    }

    private static IReadOnlyList<string> ValidateIsins(IEnumerable<string> isins)
    {
        return ValidateValues(isins, nameof(isins), "ISIN");
    }

    private static IReadOnlyList<string> ValidateTickers(IEnumerable<string> tickers)
    {
        return ValidateValues(tickers, nameof(tickers), "ticker");
    }

    private static void ValidateBaseAssetTicker(string type, string? baseAssetTicker)
    {
        if (string.IsNullOrEmpty(baseAssetTicker))
        {
            if (string.Equals(type, BcsInstrumentTypes.Options, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Base asset ticker is required when instrument type is OPTIONS.",
                    nameof(baseAssetTicker));
            }
        }
    }

    private static IReadOnlyList<string> ValidateValues(
        IEnumerable<string> values,
        string parameterName,
        string displayName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);

        var requestedValues = values.ToArray();

        if (requestedValues.Length == 0)
        {
            throw new ArgumentException($"At least one {displayName} is required.", parameterName);
        }

        if (requestedValues.Any(string.IsNullOrEmpty))
        {
            throw new ArgumentException($"{displayName} values must not be null or empty.", parameterName);
        }

        return requestedValues;
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
        if (size is < 1 or > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(size),
                size,
                "Page size must be between 1 and 100.");
        }
    }

    private sealed record BcsInstrumentsByIsinsRequest(IReadOnlyList<string> Isins);

    private sealed record BcsInstrumentsByTickersRequest(IReadOnlyList<string> Tickers);
}

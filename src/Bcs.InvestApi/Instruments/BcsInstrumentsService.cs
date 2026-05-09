namespace Bcs.InvestApi.Instruments;

using System.Globalization;
using System.Text;
using System.Text.Json;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Tokens;

internal sealed class BcsInstrumentsService
{
    private const string InstrumentsByIsinsPath = "trade-api-information-service/api/v1/instruments/by-isins";
    private const string InstrumentsByTickersPath = "trade-api-information-service/api/v1/instruments/by-tickers";
    private const string InstrumentsByTypePath = "trade-api-information-service/api/v1/instruments/by-type";

    private readonly BcsApiRequestExecutor _executor;
    private readonly Uri _instrumentsByIsinsUrl;
    private readonly Uri _instrumentsByTickersUrl;
    private readonly Uri _instrumentsByTypeUrl;

    internal BcsInstrumentsService(
        BcsInvestApiSettings settings,
        HttpClient httpClient,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
        : this(settings, new BcsApiRequestExecutor(httpClient, tokens, requestSender))
    {
    }

    internal BcsInstrumentsService(
        BcsInvestApiSettings settings,
        Func<HttpClient> httpClientFactory,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
        : this(settings, new BcsApiRequestExecutor(httpClientFactory, tokens, requestSender))
    {
    }

    private BcsInstrumentsService(
        BcsInvestApiSettings settings,
        BcsApiRequestExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.ValidateTransportSettings();

        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _instrumentsByIsinsUrl = settings.CreateEndpointUrl(InstrumentsByIsinsPath);
        _instrumentsByTickersUrl = settings.CreateEndpointUrl(InstrumentsByTickersPath);
        _instrumentsByTypeUrl = settings.CreateEndpointUrl(InstrumentsByTypePath);
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

        return await SendPageAsync(
            requestedIsins,
            page,
            size,
            CreateIsinsRequestMessage,
            "instruments-by-isins",
            cancellationToken)
            .ConfigureAwait(false);
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

        return await SendPageAsync(
            requestedTickers,
            page,
            size,
            CreateTickersRequestMessage,
            "instruments-by-tickers",
            cancellationToken)
            .ConfigureAwait(false);
    }

    internal async Task<IReadOnlyList<BcsInstrument>> GetInstrumentsByTypeAsync(
        string type,
        int page,
        int size,
        string? baseAssetTicker = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ValidatePage(page);
        ValidateSize(size);

        return await SendTypePageAsync(
            type,
            baseAssetTicker,
            page,
            size,
            cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<BcsInstrument>> SendPageAsync(
        IReadOnlyList<string> instrumentIds,
        int page,
        int size,
        Func<string, IReadOnlyList<string>, int, int, HttpRequestMessage> requestFactory,
        string endpoint,
        CancellationToken cancellationToken)
    {
        return await _executor
            .SendJsonAsync<List<BcsInstrument>>(
                accessToken => requestFactory(accessToken, instrumentIds, page, size),
                endpoint,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<BcsInstrument>> SendTypePageAsync(
        string type,
        string? baseAssetTicker,
        int page,
        int size,
        CancellationToken cancellationToken)
    {
        return await _executor
            .SendJsonAsync<List<BcsInstrument>>(
                accessToken => CreateTypeRequestMessage(accessToken, type, baseAssetTicker, page, size),
                "instruments-by-type",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private HttpRequestMessage CreateIsinsRequestMessage(
        string accessToken,
        IReadOnlyList<string> isins,
        int page,
        int size)
    {
        var requestMessage = new HttpRequestMessage(
                HttpMethod.Post,
                CreateInstrumentsByIsinsUrl(page, size))
            .WithBearer(accessToken)
            .AcceptJson();
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
        return new HttpRequestMessage(
                HttpMethod.Get,
                CreateInstrumentsByTypeUrl(type, baseAssetTicker, page, size))
            .WithBearer(accessToken)
            .AcceptJson();
    }

    private HttpRequestMessage CreateTickersRequestMessage(
        string accessToken,
        IReadOnlyList<string> tickers,
        int page,
        int size)
    {
        var requestMessage = new HttpRequestMessage(
                HttpMethod.Post,
                CreateInstrumentsByTickersUrl(page, size))
            .WithBearer(accessToken)
            .AcceptJson();
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

        if (!string.IsNullOrWhiteSpace(baseAssetTicker))
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

        if (requestedValues.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException($"{displayName} values must not be null or whitespace.", parameterName);
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
        if (size < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(size),
                size,
                "Page size must be greater than or equal to one.");
        }
    }

    private sealed record BcsInstrumentsByIsinsRequest(IReadOnlyList<string> Isins);

    private sealed record BcsInstrumentsByTickersRequest(IReadOnlyList<string> Tickers);
}

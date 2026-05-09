namespace Bcs.InvestApi.MarketData;

using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Tokens;

internal sealed class BcsMarketDataService
{
    private const string CandlesPath = "trade-api-market-data-connector/api/v1/candles-chart";

    private readonly Func<HttpClient> _httpClientFactory;
    private readonly bool _disposeHttpClientAfterRequest;
    private readonly Uri _candlesUrl;
    private readonly IBcsHttpSender _requestSender;
    private readonly IBcsAccessTokenProvider _tokens;

    internal BcsMarketDataService(
        BcsInvestApiSettings settings,
        HttpClient httpClient,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
        : this(settings, () => httpClient, tokens, requestSender, disposeHttpClientAfterRequest: false)
    {
    }

    internal BcsMarketDataService(
        BcsInvestApiSettings settings,
        Func<HttpClient> httpClientFactory,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
        : this(settings, httpClientFactory, tokens, requestSender, disposeHttpClientAfterRequest: true)
    {
    }

    private BcsMarketDataService(
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
        _candlesUrl = settings.CreateEndpointUrl(CandlesPath);
        _disposeHttpClientAfterRequest = disposeHttpClientAfterRequest;
    }

    internal async Task<BcsCandlesResponse> GetCandlesAsync(
        string classCode,
        string ticker,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        string timeFrame,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(classCode);
        ArgumentException.ThrowIfNullOrEmpty(ticker);
        ArgumentException.ThrowIfNullOrEmpty(timeFrame);
        ValidateDateRange(startDate, endDate);

        var httpClient = _httpClientFactory();

        try
        {
            var responseBody = await BcsReadApiRequestExecutor
                .SendAsync(
                    httpClient,
                    _tokens,
                    _requestSender,
                    accessToken => CreateRequestMessage(
                        accessToken,
                        classCode,
                        ticker,
                        startDate,
                        endDate,
                        timeFrame),
                    "candles-chart",
                    cancellationToken)
                .ConfigureAwait(false);

            var candles = JsonSerializer.Deserialize<BcsCandlesResponse>(
                responseBody,
                BcsJson.SerializerOptions);

            if (candles is null)
            {
                throw new JsonException("BCS candles response body is empty or cannot be deserialized.");
            }

            return candles;
        }
        finally
        {
            if (_disposeHttpClientAfterRequest)
            {
                httpClient.Dispose();
            }
        }
    }

    private HttpRequestMessage CreateRequestMessage(
        string accessToken,
        string classCode,
        string ticker,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        string timeFrame)
    {
        var requestMessage = new HttpRequestMessage(
            HttpMethod.Get,
            CreateCandlesUrl(classCode, ticker, startDate, endDate, timeFrame));

        requestMessage.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return requestMessage;
    }

    private Uri CreateCandlesUrl(
        string classCode,
        string ticker,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        string timeFrame)
    {
        var query = string.Create(
            CultureInfo.InvariantCulture,
            $"classCode={Uri.EscapeDataString(classCode)}" +
            $"&ticker={Uri.EscapeDataString(ticker)}" +
            $"&startDate={Uri.EscapeDataString(FormatQueryDate(startDate))}" +
            $"&endDate={Uri.EscapeDataString(FormatQueryDate(endDate))}" +
            $"&timeFrame={Uri.EscapeDataString(timeFrame)}");

        var builder = new UriBuilder(_candlesUrl)
        {
            Query = query,
        };

        return builder.Uri;
    }

    private static void ValidateDateRange(
        DateTimeOffset startDate,
        DateTimeOffset endDate)
    {
        if (endDate <= startDate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endDate),
                endDate,
                "End date must be greater than start date.");
        }
    }

    private static string FormatQueryDate(DateTimeOffset value) =>
        value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
}

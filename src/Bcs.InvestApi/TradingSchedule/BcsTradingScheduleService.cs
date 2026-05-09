namespace Bcs.InvestApi.TradingSchedule;

using System.Net.Http.Headers;
using System.Text.Json;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Tokens;

internal sealed class BcsTradingScheduleService
{
    private const string DailySchedulePath = "trade-api-information-service/api/v1/trading-schedule/daily-schedule";

    private readonly Func<HttpClient> _httpClientFactory;
    private readonly bool _disposeHttpClientAfterRequest;
    private readonly Uri _dailyScheduleUrl;
    private readonly IBcsHttpSender _requestSender;
    private readonly IBcsAccessTokenProvider _tokens;

    internal BcsTradingScheduleService(
        BcsInvestApiSettings settings,
        HttpClient httpClient,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
        : this(settings, () => httpClient, tokens, requestSender, disposeHttpClientAfterRequest: false)
    {
    }

    internal BcsTradingScheduleService(
        BcsInvestApiSettings settings,
        Func<HttpClient> httpClientFactory,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
        : this(settings, httpClientFactory, tokens, requestSender, disposeHttpClientAfterRequest: true)
    {
    }

    private BcsTradingScheduleService(
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
        _dailyScheduleUrl = settings.CreateEndpointUrl(DailySchedulePath);
        _disposeHttpClientAfterRequest = disposeHttpClientAfterRequest;
    }

    internal async Task<BcsDailyTradingScheduleResponse> GetDailyTradingScheduleAsync(
        string classCode,
        string ticker,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(classCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(ticker);

        var httpClient = _httpClientFactory();

        try
        {
            var responseBody = await BcsReadApiRequestExecutor
                .SendAsync(
                    httpClient,
                    _tokens,
                    _requestSender,
                    accessToken => CreateRequestMessage(accessToken, classCode, ticker),
                    "daily-trading-schedule",
                    cancellationToken)
                .ConfigureAwait(false);

            var schedule = JsonSerializer.Deserialize<BcsDailyTradingScheduleResponse>(
                responseBody,
                BcsJson.SerializerOptions);

            if (schedule is null)
            {
                throw new JsonException("BCS daily trading schedule response body is empty or cannot be deserialized.");
            }

            return schedule;
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
        string ticker)
    {
        var requestMessage = new HttpRequestMessage(
            HttpMethod.Get,
            CreateDailyScheduleUrl(classCode, ticker));

        requestMessage.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return requestMessage;
    }

    private Uri CreateDailyScheduleUrl(string classCode, string ticker)
    {
        var query = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"classCode={Uri.EscapeDataString(classCode)}&ticker={Uri.EscapeDataString(ticker)}");

        var builder = new UriBuilder(_dailyScheduleUrl)
        {
            Query = query,
        };

        return builder.Uri;
    }
}

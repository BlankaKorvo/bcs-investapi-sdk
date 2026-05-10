namespace Bcs.InvestApi.Services;

using System.Globalization;
using Bcs.InvestApi;
using Bcs.InvestApi.Contracts.Enums;
using Bcs.InvestApi.Contracts.MarketData;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Tokens;

internal sealed class BcsMarketDataService
{
    private readonly Uri _candlesUrl;
    private readonly BcsApiRequestExecutor _executor;

    internal BcsMarketDataService(
        BcsInvestApiSettings settings,
        HttpClient httpClient,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
        : this(settings, new BcsApiRequestExecutor(httpClient, tokens, requestSender))
    {
    }

    internal BcsMarketDataService(
        BcsInvestApiSettings settings,
        Func<HttpClient> httpClientFactory,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
        : this(settings, new BcsApiRequestExecutor(httpClientFactory, tokens, requestSender))
    {
    }

    private BcsMarketDataService(
        BcsInvestApiSettings settings,
        BcsApiRequestExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.ValidateBaseUrl();

        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _candlesUrl = settings.CreateEndpointUrl(BcsEndpointPaths.MarketData.Candles);
    }

    internal Task<BcsCandlesResponse> GetCandlesAsync(
        string classCode,
        string ticker,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        BcsCandleTimeFrames timeFrame,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(classCode);
        ArgumentException.ThrowIfNullOrEmpty(ticker);
        var timeFrameValue = timeFrame.ToApiValue();
        ValidateDateRange(startDate, endDate);

        return _executor.SendJsonAsync<BcsCandlesResponse>(
            accessToken => CreateRequestMessage(
                accessToken,
                classCode,
                ticker,
                startDate,
                endDate,
                timeFrameValue),
            "candles-chart",
            cancellationToken);
    }

    private HttpRequestMessage CreateRequestMessage(
        string accessToken,
        string classCode,
        string ticker,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        string timeFrame)
    {
        return new HttpRequestMessage(
                HttpMethod.Get,
                CreateCandlesUrl(classCode, ticker, startDate, endDate, timeFrame))
            .WithBearer(accessToken)
            .AcceptJson();
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

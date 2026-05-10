namespace Bcs.InvestApi.Services;

using Bcs.InvestApi;
using Bcs.InvestApi.Contracts.TradingSchedule;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Tokens;

internal sealed class BcsTradingScheduleService
{
    private readonly Uri _dailyScheduleUrl;
    private readonly BcsApiRequestExecutor _executor;

    internal BcsTradingScheduleService(
        BcsInvestApiSettings settings,
        HttpClient httpClient,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
        : this(settings, new BcsApiRequestExecutor(httpClient, tokens, requestSender))
    {
    }

    internal BcsTradingScheduleService(
        BcsInvestApiSettings settings,
        Func<HttpClient> httpClientFactory,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
        : this(settings, new BcsApiRequestExecutor(httpClientFactory, tokens, requestSender))
    {
    }

    private BcsTradingScheduleService(
        BcsInvestApiSettings settings,
        BcsApiRequestExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.ValidateTransportSettings();

        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _dailyScheduleUrl = settings.CreateEndpointUrl(BcsEndpointPaths.TradingSchedule.DailySchedule);
    }

    internal Task<BcsDailyTradingScheduleResponse> GetDailyTradingScheduleAsync(
        string classCode,
        string ticker,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(classCode);
        ArgumentException.ThrowIfNullOrEmpty(ticker);

        return _executor.SendJsonAsync<BcsDailyTradingScheduleResponse>(
            accessToken => CreateRequestMessage(accessToken, classCode, ticker),
            "daily-trading-schedule",
            cancellationToken);
    }

    private HttpRequestMessage CreateRequestMessage(
        string accessToken,
        string classCode,
        string ticker)
    {
        return new HttpRequestMessage(
                HttpMethod.Get,
                CreateDailyScheduleUrl(classCode, ticker))
            .WithBearer(accessToken)
            .AcceptJson();
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

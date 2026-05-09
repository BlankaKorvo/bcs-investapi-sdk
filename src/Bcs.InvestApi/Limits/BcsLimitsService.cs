namespace Bcs.InvestApi.Limits;

using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Tokens;

internal sealed class BcsLimitsService
{
    private const string LimitsPath = "trade-api-bff-limit/api/v1/limits";

    private readonly BcsApiRequestExecutor _executor;
    private readonly Uri _limitsUrl;

    internal BcsLimitsService(
        BcsInvestApiSettings settings,
        HttpClient httpClient,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
        : this(settings, new BcsApiRequestExecutor(httpClient, tokens, requestSender))
    {
    }

    internal BcsLimitsService(
        BcsInvestApiSettings settings,
        Func<HttpClient> httpClientFactory,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
        : this(settings, new BcsApiRequestExecutor(httpClientFactory, tokens, requestSender))
    {
    }

    private BcsLimitsService(
        BcsInvestApiSettings settings,
        BcsApiRequestExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.ValidateTransportSettings();

        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _limitsUrl = settings.CreateEndpointUrl(LimitsPath);
    }

    internal Task<BcsLimitsResponse> GetLimitsAsync(CancellationToken cancellationToken = default) =>
        _executor.SendJsonAsync<BcsLimitsResponse>(CreateRequestMessage, "limits", cancellationToken);

    private HttpRequestMessage CreateRequestMessage(string accessToken) =>
        new HttpRequestMessage(HttpMethod.Get, _limitsUrl)
            .WithBearer(accessToken)
            .AcceptJson();
}

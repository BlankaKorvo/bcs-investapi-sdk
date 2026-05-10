namespace Bcs.InvestApi.Services;

using Bcs.InvestApi;
using Bcs.InvestApi.Contracts.Portfolio;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Tokens;

internal sealed class BcsPortfolioService
{
    private readonly BcsApiRequestExecutor _executor;
    private readonly Uri _portfolioUrl;

    internal BcsPortfolioService(
        BcsInvestApiSettings settings,
        HttpClient httpClient,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
        : this(settings, new BcsApiRequestExecutor(httpClient, tokens, requestSender))
    {
    }

    internal BcsPortfolioService(
        BcsInvestApiSettings settings,
        Func<HttpClient> httpClientFactory,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
        : this(settings, new BcsApiRequestExecutor(httpClientFactory, tokens, requestSender))
    {
    }

    private BcsPortfolioService(
        BcsInvestApiSettings settings,
        BcsApiRequestExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.ValidateTransportSettings();

        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _portfolioUrl = settings.CreateEndpointUrl(BcsEndpointPaths.Portfolio);
    }

    internal async Task<IReadOnlyList<BcsPortfolioItem>> GetPortfolioAsync(CancellationToken cancellationToken = default) =>
        await _executor
            .SendJsonAsync<List<BcsPortfolioItem>>(CreateRequestMessage, "portfolio", cancellationToken)
            .ConfigureAwait(false);

    private HttpRequestMessage CreateRequestMessage(string accessToken) =>
        new HttpRequestMessage(HttpMethod.Get, _portfolioUrl)
            .WithBearer(accessToken)
            .AcceptJson();
}

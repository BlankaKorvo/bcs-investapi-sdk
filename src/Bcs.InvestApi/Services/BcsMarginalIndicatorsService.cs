namespace Bcs.InvestApi.Services;

using Bcs.InvestApi;
using Bcs.InvestApi.Contracts.MarginalIndicators;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Tokens;

internal sealed class BcsMarginalIndicatorsService : IBcsMarginalIndicatorsApi
{
    private readonly Uri _instrumentsDiscountsUrl;
    private readonly BcsApiRequestExecutor _executor;

    internal BcsMarginalIndicatorsService(
        BcsInvestApiSettings settings,
        HttpClient httpClient,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
        : this(settings, new BcsApiRequestExecutor(httpClient, tokens, requestSender))
    {
    }

    internal BcsMarginalIndicatorsService(
        BcsInvestApiSettings settings,
        Func<HttpClient> httpClientFactory,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
        : this(settings, new BcsApiRequestExecutor(httpClientFactory, tokens, requestSender))
    {
    }

    private BcsMarginalIndicatorsService(
        BcsInvestApiSettings settings,
        BcsApiRequestExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.ValidateBaseUrl();

        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _instrumentsDiscountsUrl = settings.CreateEndpointUrl(BcsEndpointPaths.MarginalIndicators.InstrumentsDiscounts);
    }

    public async Task<IReadOnlyList<BcsInstrumentDiscount>> GetInstrumentsDiscountsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _executor
            .SendJsonAsync<List<BcsInstrumentDiscount>>(
                accessToken => new HttpRequestMessage(HttpMethod.Get, _instrumentsDiscountsUrl)
                    .WithBearer(accessToken)
                    .AcceptJson(),
                "instruments-discounts",
                cancellationToken)
            .ConfigureAwait(false);
    }
}

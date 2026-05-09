namespace Bcs.InvestApi.Portfolio;

using System.Net.Http.Headers;
using System.Text.Json;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Tokens;

internal sealed class BcsPortfolioService
{
    private const string PortfolioPath = "trade-api-bff-portfolio/api/v1/portfolio";

    private readonly Func<HttpClient> _httpClientFactory;
    private readonly bool _disposeHttpClientAfterRequest;
    private readonly Uri _portfolioUrl;
    private readonly IBcsHttpSender _requestSender;
    private readonly IBcsAccessTokenProvider _tokens;

    internal BcsPortfolioService(
        BcsInvestApiSettings settings,
        HttpClient httpClient,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
        : this(settings, () => httpClient, tokens, requestSender, disposeHttpClientAfterRequest: false)
    {
    }

    internal BcsPortfolioService(
        BcsInvestApiSettings settings,
        Func<HttpClient> httpClientFactory,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
        : this(settings, httpClientFactory, tokens, requestSender, disposeHttpClientAfterRequest: true)
    {
    }

    private BcsPortfolioService(
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
        _portfolioUrl = settings.CreateEndpointUrl(PortfolioPath);
        _disposeHttpClientAfterRequest = disposeHttpClientAfterRequest;
    }

    internal async Task<IReadOnlyList<BcsPortfolioItem>> GetPortfolioAsync(CancellationToken cancellationToken = default)
    {
        var httpClient = _httpClientFactory();

        try
        {
            var responseBody = await BcsReadApiRequestExecutor
                .SendAsync(httpClient, _tokens, _requestSender, CreateRequestMessage, "portfolio", cancellationToken)
                .ConfigureAwait(false);

            var portfolio = JsonSerializer.Deserialize<List<BcsPortfolioItem>>(
                responseBody,
                BcsJson.SerializerOptions);

            if (portfolio is null)
            {
                throw new JsonException("BCS portfolio response body is empty or cannot be deserialized.");
            }

            return portfolio;
        }
        finally
        {
            if (_disposeHttpClientAfterRequest)
            {
                httpClient.Dispose();
            }
        }
    }

    private HttpRequestMessage CreateRequestMessage(string accessToken)
    {
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, _portfolioUrl);

        requestMessage.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return requestMessage;
    }
}

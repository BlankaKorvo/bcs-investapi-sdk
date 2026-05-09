namespace Bcs.InvestApi.Portfolio;

using System.Net.Http.Headers;
using System.Text.Json;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Tokens;

internal sealed class BcsPortfolioService
{
    private static readonly Uri PortfolioUrl = new("https://be.broker.ru/trade-api-bff-portfolio/api/v1/portfolio");

    private readonly Func<HttpClient> _httpClientFactory;
    private readonly bool _disposeHttpClientAfterRequest;
    private readonly IBcsReadHttpSender _requestSender;
    private readonly IBcsAccessTokenProvider _tokens;

    internal BcsPortfolioService(
        HttpClient httpClient,
        IBcsAccessTokenProvider tokens,
        IBcsReadHttpSender requestSender)
        : this(() => httpClient, tokens, requestSender, disposeHttpClientAfterRequest: false)
    {
    }

    internal BcsPortfolioService(
        Func<HttpClient> httpClientFactory,
        IBcsAccessTokenProvider tokens,
        IBcsReadHttpSender requestSender)
        : this(httpClientFactory, tokens, requestSender, disposeHttpClientAfterRequest: true)
    {
    }

    private BcsPortfolioService(
        Func<HttpClient> httpClientFactory,
        IBcsAccessTokenProvider tokens,
        IBcsReadHttpSender requestSender,
        bool disposeHttpClientAfterRequest)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _requestSender = requestSender ?? throw new ArgumentNullException(nameof(requestSender));
        _disposeHttpClientAfterRequest = disposeHttpClientAfterRequest;
    }

    internal async Task<IReadOnlyList<BcsPortfolioPosition>> GetPortfolioAsync(CancellationToken cancellationToken = default)
    {
        var accessToken = await _tokens.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        var httpClient = _httpClientFactory();

        try
        {
            using var exchange = await _requestSender
                .SendAsync(httpClient, () => CreateRequestMessage(accessToken), cancellationToken)
                .ConfigureAwait(false);
            var response = exchange.Response;

            var responseBody = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var portfolio = JsonSerializer.Deserialize<List<BcsPortfolioPosition>>(
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

    private static HttpRequestMessage CreateRequestMessage(string accessToken)
    {
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, PortfolioUrl);

        requestMessage.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return requestMessage;
    }
}

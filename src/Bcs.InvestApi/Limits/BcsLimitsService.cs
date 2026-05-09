namespace Bcs.InvestApi.Limits;

using System.Net.Http.Headers;
using System.Text.Json;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Tokens;

internal sealed class BcsLimitsService
{
    private static readonly Uri LimitsUrl = new("https://be.broker.ru/trade-api-bff-limit/api/v1/limits");

    private readonly Func<HttpClient> _httpClientFactory;
    private readonly bool _disposeHttpClientAfterRequest;
    private readonly IBcsReadHttpSender _requestSender;
    private readonly IBcsAccessTokenProvider _tokens;

    internal BcsLimitsService(
        HttpClient httpClient,
        IBcsAccessTokenProvider tokens,
        IBcsReadHttpSender requestSender)
        : this(() => httpClient, tokens, requestSender, disposeHttpClientAfterRequest: false)
    {
    }

    internal BcsLimitsService(
        Func<HttpClient> httpClientFactory,
        IBcsAccessTokenProvider tokens,
        IBcsReadHttpSender requestSender)
        : this(httpClientFactory, tokens, requestSender, disposeHttpClientAfterRequest: true)
    {
    }

    private BcsLimitsService(
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

    internal async Task<BcsLimitsResponse> GetLimitsAsync(CancellationToken cancellationToken = default)
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

            var limits = JsonSerializer.Deserialize<BcsLimitsResponse>(
                responseBody,
                BcsJson.SerializerOptions);

            if (limits is null)
            {
                throw new JsonException("BCS limits response body is empty or cannot be deserialized.");
            }

            return limits;
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
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, LimitsUrl);

        requestMessage.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return requestMessage;
    }
}

namespace Bcs.InvestApi.Limits;

using System.Net.Http.Headers;
using System.Text.Json;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Tokens;

internal sealed class BcsLimitsService
{
    private const string LimitsPath = "trade-api-bff-limit/api/v1/limits";

    private readonly Func<HttpClient> _httpClientFactory;
    private readonly bool _disposeHttpClientAfterRequest;
    private readonly Uri _limitsUrl;
    private readonly IBcsHttpSender _requestSender;
    private readonly IBcsAccessTokenProvider _tokens;

    internal BcsLimitsService(
        BcsInvestApiSettings settings,
        HttpClient httpClient,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
        : this(settings, () => httpClient, tokens, requestSender, disposeHttpClientAfterRequest: false)
    {
    }

    internal BcsLimitsService(
        BcsInvestApiSettings settings,
        Func<HttpClient> httpClientFactory,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
        : this(settings, httpClientFactory, tokens, requestSender, disposeHttpClientAfterRequest: true)
    {
    }

    private BcsLimitsService(
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
        _limitsUrl = settings.CreateEndpointUrl(LimitsPath);
        _disposeHttpClientAfterRequest = disposeHttpClientAfterRequest;
    }

    internal async Task<BcsLimitsResponse> GetLimitsAsync(CancellationToken cancellationToken = default)
    {
        var httpClient = _httpClientFactory();

        try
        {
            var responseBody = await BcsReadApiRequestExecutor
                .SendAsync(httpClient, _tokens, _requestSender, CreateRequestMessage, "limits", cancellationToken)
                .ConfigureAwait(false);

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

    private HttpRequestMessage CreateRequestMessage(string accessToken)
    {
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, _limitsUrl);

        requestMessage.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return requestMessage;
    }
}

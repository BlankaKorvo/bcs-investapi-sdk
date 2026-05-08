namespace Bcs.InvestApi.Auth;

using System.Net.Http.Headers;
using System.Text.Json;
using Bcs.InvestApi.Infrastructure;
using Microsoft.Extensions.Options;

public sealed class BcsAuthService
{
    private readonly Func<HttpClient> _httpClientFactory;
    private readonly bool _disposeHttpClientAfterRequest;
    private readonly IBcsAuthHttpSender _requestSender;
    private readonly BcsInvestApiSettings _settings;

    public BcsAuthService(HttpClient httpClient, IOptions<BcsInvestApiSettings> options)
        : this(httpClient, options.Value)
    {
    }

    public BcsAuthService(HttpClient httpClient, BcsInvestApiSettings settings)
        : this(() => httpClient, settings, disposeHttpClientAfterRequest: false)
    {
    }

    internal BcsAuthService(Func<HttpClient> httpClientFactory, BcsInvestApiSettings settings)
        : this(httpClientFactory, settings, disposeHttpClientAfterRequest: true)
    {
    }

    internal BcsAuthService(
        HttpClient httpClient,
        BcsInvestApiSettings settings,
        IBcsAuthHttpSender requestSender)
        : this(() => httpClient, settings, disposeHttpClientAfterRequest: false, requestSender)
    {
    }

    internal BcsAuthService(
        Func<HttpClient> httpClientFactory,
        BcsInvestApiSettings settings,
        IBcsAuthHttpSender requestSender)
        : this(httpClientFactory, settings, disposeHttpClientAfterRequest: true, requestSender)
    {
    }

    private BcsAuthService(
        Func<HttpClient> httpClientFactory,
        BcsInvestApiSettings settings,
        bool disposeHttpClientAfterRequest,
        IBcsAuthHttpSender? requestSender = null)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _disposeHttpClientAfterRequest = disposeHttpClientAfterRequest;
        _requestSender = requestSender ?? new BcsAuthRequestSender(settings);

        _settings.ValidateTransportSettings();
    }

    public async Task<BcsAuthResponse> GetAccessTokenAsync(
        BcsAuthRequest authRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authRequest);
        authRequest.Validate();

        var httpClient = _httpClientFactory();

        try
        {
            using var exchange = await _requestSender
                .SendAsync(httpClient, () => CreateRequestMessage(authRequest), cancellationToken)
                .ConfigureAwait(false);
            var response = exchange.Response;

            var responseBody = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var error = TryDeserializeError(responseBody);

                throw new BcsAuthException(
                    response.StatusCode,
                    error?.Error,
                    error?.ErrorDescription,
                    responseBody);
            }

            var authResponse = JsonSerializer.Deserialize<BcsAuthResponse>(
                responseBody,
                BcsJson.SerializerOptions);

            if (authResponse is null)
            {
                throw new JsonException("BCS auth response body is empty or cannot be deserialized.");
            }

            authResponse.Validate();

            return authResponse;
        }
        finally
        {
            if (_disposeHttpClientAfterRequest)
            {
                httpClient.Dispose();
            }
        }
    }

    private HttpRequestMessage CreateRequestMessage(BcsAuthRequest authRequest)
    {
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, _settings.AuthUrl);

        requestMessage.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        requestMessage.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = authRequest.ClientId,
            ["refresh_token"] = authRequest.RefreshToken,
            ["grant_type"] = authRequest.GrantType,
        });

        return requestMessage;
    }

    private static BcsAuthErrorResponse? TryDeserializeError(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<BcsAuthErrorResponse>(responseBody, BcsJson.SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

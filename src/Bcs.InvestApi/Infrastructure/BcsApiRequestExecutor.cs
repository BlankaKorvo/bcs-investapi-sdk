namespace Bcs.InvestApi.Infrastructure;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Bcs.InvestApi.Contracts.Exceptions;
using Bcs.InvestApi.Tokens;

internal sealed class BcsApiRequestExecutor
{
    private readonly Func<HttpClient> _httpClientFactory;
    private readonly bool _disposeHttpClientAfterRequest;
    private readonly IBcsHttpSender _requestSender;
    private readonly IBcsAccessTokenProvider _tokens;

    internal BcsApiRequestExecutor(
        HttpClient httpClient,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
        : this(CreateHttpClientFactory(httpClient), tokens, requestSender, disposeHttpClientAfterRequest: false)
    {
    }

    internal BcsApiRequestExecutor(
        Func<HttpClient> httpClientFactory,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
        : this(httpClientFactory, tokens, requestSender, disposeHttpClientAfterRequest: true)
    {
    }

    private BcsApiRequestExecutor(
        Func<HttpClient> httpClientFactory,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender,
        bool disposeHttpClientAfterRequest)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _requestSender = requestSender ?? throw new ArgumentNullException(nameof(requestSender));
        _disposeHttpClientAfterRequest = disposeHttpClientAfterRequest;
    }

    public async Task<TResponse> SendJsonAsync<TResponse>(
        Func<string, HttpRequestMessage> requestFactory,
        string endpointName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);

        var httpClient = _httpClientFactory();
        ArgumentNullException.ThrowIfNull(httpClient);

        try
        {
            var responseBody = await SendAsync(
                httpClient,
                requestFactory,
                endpointName,
                cancellationToken)
                .ConfigureAwait(false);

            var response = JsonSerializer.Deserialize<TResponse>(
                responseBody,
                BcsJson.SerializerOptions);

            if (response is null)
            {
                throw new JsonException($"BCS {endpointName} response body is empty or cannot be deserialized.");
            }

            return response;
        }
        finally
        {
            if (_disposeHttpClientAfterRequest)
            {
                httpClient.Dispose();
            }
        }
    }

    private static Func<HttpClient> CreateHttpClientFactory(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        return () => httpClient;
    }

    private async Task<string> SendAsync(
        HttpClient httpClient,
        Func<string, HttpRequestMessage> requestFactory,
        string endpointName,
        CancellationToken cancellationToken)
    {
        var accessToken = await _tokens
            .GetAccessTokenAsync(cancellationToken)
            .ConfigureAwait(false);
        using var exchange = await _requestSender
            .SendAsync(httpClient, () => requestFactory(accessToken), cancellationToken)
            .ConfigureAwait(false);
        var response = exchange.Response;

        var responseBody = await response.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized &&
            IsInvalidToken(response))
        {
            _tokens.InvalidateAccessToken(accessToken);
        }

        EnsureSuccess(response.StatusCode, responseBody, endpointName);
        return responseBody;
    }

    private static void EnsureSuccess(HttpStatusCode statusCode, string responseBody, string endpointName)
    {
        var statusCodeValue = (int)statusCode;
        if (statusCodeValue is >= 200 and <= 299)
        {
            return;
        }

        throw new BcsApiException(statusCode, responseBody, endpointName);
    }

    private static bool IsInvalidToken(HttpResponseMessage response) =>
        response.Headers.WwwAuthenticate.Any(IsInvalidTokenChallenge);

    private static bool IsInvalidTokenChallenge(AuthenticationHeaderValue challenge)
    {
        if (!string.Equals(challenge.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(challenge.Parameter))
        {
            return false;
        }

        return challenge.Parameter
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(IsInvalidTokenParameter);
    }

    private static bool IsInvalidTokenParameter(string parameter)
    {
        const string prefix = "error=";

        if (!parameter.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var value = parameter[prefix.Length..].Trim().Trim('"');
        return string.Equals(value, "invalid_token", StringComparison.Ordinal);
    }
}

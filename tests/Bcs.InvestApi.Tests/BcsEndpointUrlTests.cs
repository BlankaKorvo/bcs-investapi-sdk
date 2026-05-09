namespace Bcs.InvestApi.Tests;

using System.Net;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Limits;
using Bcs.InvestApi.Portfolio;
using Bcs.InvestApi.Tests.Infrastructure;
using Bcs.InvestApi.Tokens;
using Xunit;

public sealed class BcsEndpointUrlTests
{
    [Fact]
    public async Task GetLimitsAsync_UsesConfiguredBaseUrl()
    {
        var settings = CreateSettings(new Uri("https://mock.example/root"));
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, "{}")));
        var service = new BcsLimitsService(
            settings,
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsReadHttpSender(settings));

        await service.GetLimitsAsync();

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(
            new Uri("https://mock.example/root/trade-api-bff-limit/api/v1/limits"),
            handler.LastRequest.RequestUri);
    }

    [Fact]
    public async Task GetPortfolioAsync_UsesConfiguredBaseUrl()
    {
        var settings = CreateSettings(new Uri("https://mock.example/root/"));
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, "[]")));
        var service = new BcsPortfolioService(
            settings,
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsReadHttpSender(settings));

        await service.GetPortfolioAsync();

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(
            new Uri("https://mock.example/root/trade-api-bff-portfolio/api/v1/portfolio"),
            handler.LastRequest.RequestUri);
    }

    [Fact]
    public void ValidateTransportSettings_WithHttpBaseUrl_ThrowsByDefault()
    {
        var settings = CreateSettings(new Uri("http://localhost:8080"));

        var exception = Assert.Throws<InvalidOperationException>(settings.ValidateTransportSettings);

        Assert.Contains("BCS base URL", exception.Message);
        Assert.Contains("absolute HTTPS URI", exception.Message);
        Assert.Contains("http://localhost:8080", exception.Message);
    }

    [Fact]
    public void CreateEndpointUrl_WithHttpBaseUrlAndTestingOptIn_UsesLocalBaseUrl()
    {
        var settings = CreateSettings(new Uri("http://localhost:8080"));
        settings.AllowInsecureHttpForTesting = true;

        Assert.Equal(
            new Uri("http://localhost:8080/trade-api-bff-limit/api/v1/limits"),
            settings.CreateEndpointUrl("trade-api-bff-limit/api/v1/limits"));
    }

    private static BcsInvestApiSettings CreateSettings(Uri baseUrl) =>
        new()
        {
            BaseUrl = baseUrl,
            HttpRetryAttempts = 0,
        };

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };

    private sealed class StaticTokenProvider : IBcsAccessTokenProvider
    {
        private readonly string _accessToken;

        public StaticTokenProvider(string accessToken)
        {
            _accessToken = accessToken;
        }

        public ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_accessToken);
    }
}

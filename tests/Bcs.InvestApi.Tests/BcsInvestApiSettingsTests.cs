namespace Bcs.InvestApi.Tests;

using Bcs.InvestApi.Contracts.Enums;
using Bcs.InvestApi.Tests.Infrastructure;
using Xunit;

public sealed class BcsInvestApiSettingsTests
{
    [Fact]
    public void ValidateAuthClientId_WithUnsupportedClientId_ThrowsArgumentOutOfRangeException()
    {
        var settings = CreateSettings();
        settings.ClientId = (BcsAuthClientIds)999;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(settings.ValidateAuthClientId);

        Assert.Equal("clientId", exception.ParamName);
    }

    [Fact]
    public void CreateFactory_WithUnsupportedClientId_ThrowsArgumentOutOfRangeException()
    {
        var settings = new BcsInvestApiSettings
        {
            RefreshToken = "settings-refresh-1",
            ClientId = (BcsAuthClientIds)999,
            AuthUrl = new Uri("https://example.test/token"),
            BaseUrl = new Uri("https://example.test"),
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => BcsInvestApiClientFactory.Create(settings));

        Assert.Equal("clientId", exception.ParamName);
    }

    // The client id decides whether the client may trade, so neither way of creating a client has a default.
    [Fact]
    public void ClientId_HasNoDefault_AndSettingsWithoutItAreRejected()
    {
        var settings = new BcsInvestApiSettings();

        Assert.Null(settings.ClientId);
        var exception = Assert.Throws<InvalidOperationException>(settings.ValidateAuthClientId);
        Assert.Contains("Bcs:ClientId", exception.Message);
    }

    [Fact]
    public void CreateFactory_WithoutClientId_ThrowsBeforeAnyRequest()
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)));
        var settings = new BcsInvestApiSettings
        {
            RefreshToken = "settings-refresh-1",
            AuthUrl = new Uri("https://example.test/token"),
            BaseUrl = new Uri("https://example.test"),
        };

        Assert.Throws<InvalidOperationException>(() => BcsInvestApiClientFactory.Create(settings, handler));
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public void CreateFactory_ByRefreshToken_HasNoDefaultClientId()
    {
        var create = typeof(BcsInvestApiClientFactory).GetMethod(
            nameof(BcsInvestApiClientFactory.Create),
            new[] { typeof(string), typeof(BcsAuthClientIds), typeof(HttpMessageHandler) });

        Assert.NotNull(create);
        var clientId = Assert.Single(create!.GetParameters(), parameter => parameter.ParameterType == typeof(BcsAuthClientIds));
        Assert.False(clientId.HasDefaultValue);
    }

    [Theory]
    [InlineData(BcsAuthClientIds.TradeApiRead, "trade-api-read")]
    [InlineData(BcsAuthClientIds.TradeApiWrite, "trade-api-write")]
    public async Task CreateFactory_ByRefreshToken_SendsTheGivenClientId(BcsAuthClientIds clientId, string wireValue)
    {
        string? authForm = null;
        var handler = new CapturingHttpMessageHandler(async (request, cancellationToken) =>
        {
            if (request.RequestUri == BcsInvestApiSettings.DefaultAuthUrl)
            {
                authForm = await request.Content!.ReadAsStringAsync(cancellationToken);
                return Json("""{"access_token":"access-1","expires_in":86400,"refresh_expires_in":7776000,"refresh_token":"refresh-2","token_type":"bearer"}""");
            }

            return Json("{}");
        });

        await using var client = BcsInvestApiClientFactory.Create("settings-refresh-1", clientId, handler);
        await client.Limits.GetLimitsAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(authForm);
        Assert.Contains($"client_id={wireValue}", authForm);
    }

    [Fact]
    public void KnownClientIds_MapToWireValues()
    {
        Assert.Equal("trade-api-read", BcsAuthClientIds.TradeApiRead.ToApiValue());
        Assert.Equal("trade-api-write", BcsAuthClientIds.TradeApiWrite.ToApiValue());
    }

    private static HttpResponseMessage Json(string json) =>
        new(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };

    private static BcsInvestApiSettings CreateSettings() =>
        new()
        {
            AuthUrl = new Uri("https://example.test/token"),
            BaseUrl = new Uri("https://example.test"),
            ClientId = BcsAuthClientIds.TradeApiRead,
        };
}

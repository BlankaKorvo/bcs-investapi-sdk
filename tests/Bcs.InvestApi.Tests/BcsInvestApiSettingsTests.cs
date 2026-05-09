namespace Bcs.InvestApi.Tests;

using Bcs.InvestApi.Auth;
using Xunit;

public sealed class BcsInvestApiSettingsTests
{
    [Fact]
    public void ValidateTransportSettings_WithUnknownNonEmptyClientId_DoesNotThrow()
    {
        var settings = CreateSettings();
        settings.ClientId = "trade-api-future";

        var exception = Record.Exception(settings.ValidateTransportSettings);

        Assert.Null(exception);
    }

    [Fact]
    public void CreateFactory_WithUnknownNonEmptyClientId_CreatesClient()
    {
        using var client = BcsInvestApiClientFactory.Create(new BcsInvestApiSettings
        {
            RefreshToken = "settings-refresh-1",
            ClientId = "trade-api-future",
            AuthUrl = new Uri("https://example.test/token"),
            BaseUrl = new Uri("https://example.test"),
        });

        Assert.NotNull(client);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateTransportSettings_WithMissingClientId_ThrowsInvalidOperationException(string? clientId)
    {
        var settings = CreateSettings();
        settings.ClientId = clientId!;

        var exception = Assert.Throws<InvalidOperationException>(settings.ValidateTransportSettings);

        Assert.Contains("client_id", exception.Message);
    }

    [Fact]
    public void KnownClientIdConstants_RemainAvailable()
    {
        Assert.Equal("trade-api-read", BcsAuthClientIds.TradeApiRead);
        Assert.Equal("trade-api-write", BcsAuthClientIds.TradeApiWrite);
    }

    private static BcsInvestApiSettings CreateSettings() =>
        new()
        {
            AuthUrl = new Uri("https://example.test/token"),
            BaseUrl = new Uri("https://example.test"),
            ClientId = BcsAuthClientIds.TradeApiRead,
        };
}

namespace Bcs.InvestApi.Tests;

using System.Reflection;
using Bcs.InvestApi.DTO;
using Bcs.InvestApi.Services;
using Bcs.InvestApi.Tokens;
using Xunit;

public sealed class BcsInvestApiClientTests
{
    [Fact]
    public void PublicSurface_DoesNotExposeRawAuthService()
    {
        var publicProperties = typeof(BcsInvestApiClient)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public);
        var publicConstructors = typeof(BcsInvestApiClient)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public);

        Assert.Empty(publicProperties);
        Assert.Null(typeof(BcsInvestApiClient).GetProperty("Auth", BindingFlags.Instance | BindingFlags.Public));
        Assert.DoesNotContain(publicProperties, property => property.PropertyType == typeof(BcsAuthService));
        Assert.DoesNotContain(
            publicConstructors,
            constructor => constructor.GetParameters().Any(parameter => parameter.ParameterType == typeof(BcsAuthService)));
    }

    [Fact]
    public void RawAuthTypes_AreInternalImplementationDetails()
    {
        Assert.False(typeof(BcsAuthService).IsPublic);
        Assert.False(typeof(BcsAuthRequest).IsPublic);
        Assert.False(typeof(BcsAuthResponse).IsPublic);
        Assert.False(typeof(BcsAuthErrorResponse).IsPublic);
    }

    [Fact]
    public void TokenTypes_AreInternalImplementationDetails()
    {
        Assert.False(typeof(BcsTokenManager).IsPublic);
        Assert.False(typeof(IBcsAccessTokenProvider).IsPublic);
    }
}

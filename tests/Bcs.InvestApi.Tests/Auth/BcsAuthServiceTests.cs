namespace Bcs.InvestApi.Tests.Auth;

using System.Net;
using Bcs.InvestApi.Auth;
using Bcs.InvestApi.Tests.Infrastructure;
using Xunit;

public sealed class BcsAuthServiceTests
{
    [Fact]
    public async Task GetAccessTokenAsync_PostsFormToConfiguredAuthUrl()
    {
        var authUrl = new Uri("https://example.test/token");
        var handler = new CapturingHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, ValidAuthResponseJson())));
        var service = CreateService(handler, authUrl);

        await service.GetAccessTokenAsync(new BcsAuthRequest
        {
            RefreshToken = "refresh-token-1",
            ClientId = BcsAuthClientIds.TradeApiWrite,
            GrantType = BcsGrantTypes.RefreshToken
        });

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Equal(authUrl, handler.LastRequest.RequestUri);
        Assert.Equal("application/x-www-form-urlencoded", handler.LastRequest.Content?.Headers.ContentType?.MediaType);
        Assert.Contains("client_id=trade-api-write", handler.LastRequestContent);
        Assert.Contains("refresh_token=refresh-token-1", handler.LastRequestContent);
        Assert.Contains("grant_type=refresh_token", handler.LastRequestContent);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ParsesSuccessfulResponse()
    {
        var handler = new CapturingHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, ValidAuthResponseJson())));
        var service = CreateService(handler);

        var response = await service.GetAccessTokenAsync(new BcsAuthRequest
        {
            RefreshToken = "refresh-token-1",
            ClientId = BcsAuthClientIds.TradeApiRead,
            GrantType = BcsGrantTypes.RefreshToken
        });

        Assert.Equal("access-token-1", response.AccessToken);
        Assert.Equal(86400, response.ExpiresIn);
        Assert.Equal(7776000, response.RefreshExpiresIn);
        Assert.Equal("refresh-token-2", response.RefreshToken);
        Assert.Equal("bearer", response.TokenType);
        Assert.Equal(0, response.NotBeforePolicy);
        Assert.Equal("session-state-1", response.SessionState);
        Assert.Equal("trade-api-read", response.Scope);
    }

    [Fact]
    public async Task GetAccessTokenAsync_TransientServerError_RetriesWithFreshRequest()
    {
        var attempt = 0;
        var observedRequests = new List<HttpRequestMessage>();
        var handler = new CapturingHttpMessageHandler((request, _) =>
        {
            observedRequests.Add(request);

            return Task.FromResult(++attempt < 3
                ? JsonResponse(HttpStatusCode.InternalServerError, """{"error":"temporary"}""")
                : JsonResponse(HttpStatusCode.OK, ValidAuthResponseJson()));
        });
        var service = CreateService(
            handler,
            configureSettings: settings => settings.HttpRetryBaseDelay = TimeSpan.Zero);

        var response = await service.GetAccessTokenAsync(new BcsAuthRequest
        {
            RefreshToken = "refresh-token-1",
            ClientId = BcsAuthClientIds.TradeApiRead,
            GrantType = BcsGrantTypes.RefreshToken
        });

        Assert.Equal("access-token-1", response.AccessToken);
        Assert.Equal(3, handler.RequestCount);
        Assert.Equal(3, observedRequests.Distinct().Count());
    }

    [Fact]
    public async Task GetAccessTokenAsync_TransientHttpException_Retries()
    {
        var attempt = 0;
        var handler = new CapturingHttpMessageHandler((_, _) =>
        {
            if (++attempt == 1)
            {
                throw new HttpRequestException("Temporary network failure.");
            }

            return Task.FromResult(JsonResponse(HttpStatusCode.OK, ValidAuthResponseJson()));
        });
        var service = CreateService(
            handler,
            configureSettings: settings => settings.HttpRetryBaseDelay = TimeSpan.Zero);

        var response = await service.GetAccessTokenAsync(new BcsAuthRequest
        {
            RefreshToken = "refresh-token-1",
            ClientId = BcsAuthClientIds.TradeApiRead,
            GrantType = BcsGrantTypes.RefreshToken
        });

        Assert.Equal("access-token-1", response.AccessToken);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenConstructedWithHttpClient_DoesNotDisposeHttpClient()
    {
        var handler = new CapturingHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, ValidAuthResponseJson())));
        var service = CreateService(handler);

        await service.GetAccessTokenAsync(new BcsAuthRequest
        {
            RefreshToken = "refresh-token-1",
            ClientId = BcsAuthClientIds.TradeApiRead,
            GrantType = BcsGrantTypes.RefreshToken
        });

        Assert.Equal(0, handler.DisposeCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_AcceptsBearerTokenTypeCase()
    {
        var handler = new CapturingHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, ValidAuthResponseJson(tokenType: "Bearer"))));
        var service = CreateService(handler);

        var response = await service.GetAccessTokenAsync(new BcsAuthRequest
        {
            RefreshToken = "refresh-token-1",
            ClientId = BcsAuthClientIds.TradeApiRead,
            GrantType = BcsGrantTypes.RefreshToken
        });

        Assert.Equal("Bearer", response.TokenType);
    }

    [Fact]
    public async Task GetAccessTokenAsync_AcceptsUnknownClientIdOnRawRequest()
    {
        var handler = new CapturingHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, ValidAuthResponseJson())));
        var service = CreateService(handler);

        await service.GetAccessTokenAsync(new BcsAuthRequest
        {
            RefreshToken = "refresh-token-1",
            ClientId = "trade-api-future",
            GrantType = BcsGrantTypes.RefreshToken
        });

        Assert.Contains("client_id=trade-api-future", handler.LastRequestContent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAccessTokenAsync_EmptyClientId_ThrowsArgumentException(string clientId)
    {
        var handler = new CapturingHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, ValidAuthResponseJson())));
        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetAccessTokenAsync(new BcsAuthRequest
            {
                RefreshToken = "refresh-token-1",
                ClientId = clientId,
                GrantType = BcsGrantTypes.RefreshToken
            }));

        Assert.Equal("ClientId", exception.ParamName);
    }

    [Fact]
    public async Task GetAccessTokenAsync_EmptySuccessfulResponse_ThrowsInvalidOperationException()
    {
        var handler = new CapturingHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "{}")));
        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetAccessTokenAsync(new BcsAuthRequest
            {
                RefreshToken = "refresh-token-1",
                ClientId = BcsAuthClientIds.TradeApiRead,
                GrantType = BcsGrantTypes.RefreshToken
            }));

        Assert.Contains("access_token", exception.Message);
    }

    [Theory]
    [InlineData("access_token")]
    [InlineData("refresh_token")]
    [InlineData("expires_in")]
    [InlineData("refresh_expires_in")]
    [InlineData("token_type")]
    public async Task GetAccessTokenAsync_InvalidSuccessfulResponse_ThrowsInvalidOperationException(string invalidField)
    {
        var handler = new CapturingHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, InvalidAuthResponseJson(invalidField))));
        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetAccessTokenAsync(new BcsAuthRequest
            {
                RefreshToken = "refresh-token-1",
                ClientId = BcsAuthClientIds.TradeApiRead,
                GrantType = BcsGrantTypes.RefreshToken
            }));

        Assert.Contains(invalidField, exception.Message);
    }

    [Fact]
    public async Task GetAccessTokenAsync_InvalidGrant_ThrowsBcsAuthException()
    {
        const string errorJson = """
        {
          "error": "invalid_grant",
          "error_description": "Refresh token expired"
        }
        """;

        var handler = new CapturingHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.BadRequest, errorJson)));
        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<BcsAuthException>(() =>
            service.GetAccessTokenAsync(new BcsAuthRequest
            {
                RefreshToken = "refresh-token-1",
                ClientId = BcsAuthClientIds.TradeApiRead,
                GrantType = BcsGrantTypes.RefreshToken
            }));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal("invalid_grant", exception.Error);
        Assert.Equal("Refresh token expired", exception.ErrorDescription);
        Assert.Contains("invalid_grant", exception.ResponseBody);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_UsesExplicitRequestValuesInsteadOfSettingsTokenValues()
    {
        var handler = new CapturingHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, ValidAuthResponseJson())));
        var settings = new BcsInvestApiSettings
        {
            RefreshToken = "settings-refresh-token",
            ClientId = BcsAuthClientIds.TradeApiRead,
            AuthUrl = new Uri("https://example.test/token"),
        };

        var service = new BcsAuthService(new HttpClient(handler), settings);

        await service.GetAccessTokenAsync(new BcsAuthRequest
        {
            RefreshToken = "refresh-token-1",
            ClientId = BcsAuthClientIds.TradeApiWrite,
            GrantType = BcsGrantTypes.RefreshToken
        });

        Assert.Contains("client_id=trade-api-write", handler.LastRequestContent);
        Assert.Contains("refresh_token=refresh-token-1", handler.LastRequestContent);
        Assert.Contains("grant_type=refresh_token", handler.LastRequestContent);
    }

    private static BcsAuthService CreateService(
        CapturingHttpMessageHandler handler,
        Uri? authUrl = null,
        Action<BcsInvestApiSettings>? configureSettings = null)
    {
        var settings = new BcsInvestApiSettings
        {
            AuthUrl = authUrl ?? new Uri("https://example.test/token"),
            ClientId = BcsAuthClientIds.TradeApiRead,
        };

        configureSettings?.Invoke(settings);

        return new BcsAuthService(new HttpClient(handler), settings);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };

    private static string ValidAuthResponseJson(string tokenType = "bearer") =>
        $$"""
        {
          "access_token": "access-token-1",
          "expires_in": 86400,
          "refresh_expires_in": 7776000,
          "refresh_token": "refresh-token-2",
          "token_type": "{{tokenType}}",
          "not-before-policy": "0",
          "session_state": "session-state-1",
          "scope": "trade-api-read"
        }
        """;

    private static string InvalidAuthResponseJson(string invalidField)
    {
        var accessToken = invalidField == "access_token" ? string.Empty : "access-token-1";
        var expiresIn = invalidField == "expires_in" ? 0 : 86400;
        var refreshExpiresIn = invalidField == "refresh_expires_in" ? 0 : 7776000;
        var refreshToken = invalidField == "refresh_token" ? string.Empty : "refresh-token-2";
        var tokenType = invalidField == "token_type" ? string.Empty : "bearer";

        return $$"""
        {
          "access_token": "{{accessToken}}",
          "expires_in": {{expiresIn}},
          "refresh_expires_in": {{refreshExpiresIn}},
          "refresh_token": "{{refreshToken}}",
          "token_type": "{{tokenType}}",
          "not-before-policy": "0",
          "session_state": "session-state-1",
          "scope": "trade-api-read"
        }
        """;
    }
}

namespace Bcs.InvestApi.Infrastructure;

using System.Net.Http.Headers;

internal static class BcsHttpRequestMessageExtensions
{
    private const string JsonMediaType = "application/json";

    public static HttpRequestMessage AcceptJson(this HttpRequestMessage requestMessage)
    {
        ArgumentNullException.ThrowIfNull(requestMessage);

        requestMessage.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(JsonMediaType));

        return requestMessage;
    }

    public static HttpRequestMessage WithBearer(this HttpRequestMessage requestMessage, string accessToken)
    {
        ArgumentNullException.ThrowIfNull(requestMessage);

        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return requestMessage;
    }
}

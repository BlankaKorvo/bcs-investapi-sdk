namespace Bcs.InvestApi.Contracts.Auth;

using System.Text.Json.Serialization;

internal sealed record BcsAuthErrorResponse
{
    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; init; }
}

namespace Bcs.InvestApi.Auth;

using System.Text.Json.Serialization;

public sealed record BcsAuthErrorResponse
{
    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; init; }
}

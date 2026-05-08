namespace Bcs.InvestApi.Infrastructure;

using System.Text.Json;

internal static class BcsJson
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
}

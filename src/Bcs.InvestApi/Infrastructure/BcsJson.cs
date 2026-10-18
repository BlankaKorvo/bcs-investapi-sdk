namespace Bcs.InvestApi.Infrastructure;

using System.Text.Json;

internal static class BcsJson
{
    /// <summary>The web defaults; a <c>date-time</c> must carry its offset (<see cref="BcsDateTimeOffsetConverter"/>).</summary>
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new BcsDateTimeOffsetConverter() },
    };
}

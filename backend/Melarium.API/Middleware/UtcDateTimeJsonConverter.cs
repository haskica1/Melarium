using System.Text.Json;
using System.Text.Json.Serialization;

namespace Melarium.API.Middleware;

/// <summary>
/// Treats all incoming DateTime values as UTC regardless of Kind.
/// Npgsql 6+ rejects Kind=Unspecified for timestamptz columns; this converter
/// normalises at the JSON boundary so the rest of the stack sees only UTC.
/// </summary>
public class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dt = reader.GetDateTime();
        return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToUniversalTime().ToString("O"));
}

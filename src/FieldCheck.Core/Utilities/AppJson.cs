using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FieldCheck.Core.Utilities;

/// <summary>
/// Centralized System.Text.Json configuration for the persisted document. I keep this in one
/// place so the file format (camelCase keys, indented, string enums, no-offset timestamps)
/// stays consistent everywhere it is read or written.
/// </summary>
public static class AppJson
{
    /// <summary>Shared, thread-safe options instance.</summary>
    public static JsonSerializerOptions Options { get; } = Create();

    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            // I deliberately keep nulls (e.g. "completedAt": null) so the file matches the
            // documented data model and is easy to read by hand.
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true));
        // Two converters: the nullable one handles DateTime? (e.g. CompletedAt) so that an empty
        // or unparseable string deserializes to null rather than DateTime.MinValue; the
        // non-nullable one handles CreatedAt/UpdatedAt.
        options.Converters.Add(new JsonNullableLocalDateTimeConverter());
        options.Converters.Add(new JsonLocalDateTimeConverter());
        return options;
    }
}

/// <summary>Shared parse/format helpers so the nullable and non-nullable converters stay in sync.</summary>
internal static class LocalDateTimeFormat
{
    public const string Format = "yyyy-MM-ddTHH:mm:ss";

    public static bool TryParse(string? text, out DateTime value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (DateTime.TryParseExact(text, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
            return true;

        // Tolerant fallback for hand-edited or older files.
        return DateTime.TryParse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal | DateTimeStyles.NoCurrentDateDefault, out value);
    }

    public static string ToText(DateTime value) => value.ToString(Format, CultureInfo.InvariantCulture);
}

/// <summary>
/// Serializes <see cref="DateTime"/> as "yyyy-MM-ddTHH:mm:ss" (no timezone offset) to match the
/// data model. Reading is lenient so that hand-edited or older files still load.
/// </summary>
public sealed class JsonLocalDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => LocalDateTimeFormat.TryParse(reader.GetString(), out var value) ? value : default;

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(LocalDateTimeFormat.ToText(value));
}

/// <summary>
/// Nullable companion converter. A JSON null — or an empty/unparseable string — deserializes to
/// null instead of being coerced to <see cref="DateTime.MinValue"/>.
/// </summary>
public sealed class JsonNullableLocalDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        return LocalDateTimeFormat.TryParse(reader.GetString(), out var value) ? value : null;
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(LocalDateTimeFormat.ToText(value.Value));
    }
}

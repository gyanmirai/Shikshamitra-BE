using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShikshaMitra.Common;

/// <summary>
/// JSON converter that treats an empty string as null for nullable enums.
/// Handles frontend sending "" instead of null for optional enum fields.
/// </summary>
public class NullableEnumConverter<T> : JsonConverter<T?> where T : struct, Enum
{
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (string.IsNullOrEmpty(str))
                return null;
            if (Enum.TryParse<T>(str, ignoreCase: true, out var parsed))
                return parsed;
            return null;
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            var number = reader.GetInt32();
            if (Enum.IsDefined(typeof(T), number))
                return (T) (object) number;
            return null;
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteNumberValue(Convert.ToInt32(value));
    }
}

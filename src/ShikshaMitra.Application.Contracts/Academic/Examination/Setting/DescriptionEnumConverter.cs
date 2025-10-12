using System;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShikshaMitra.Common.Converters
{
	public class DescriptionEnumConverter<T> : JsonConverter<T> where T : Enum
	{
		public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			string value = null;
			if (reader.TokenType == JsonTokenType.String)
			{
				value = reader.GetString();
			}
			else if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int intValue))
			{
				return (T)Enum.ToObject(typeof(T), intValue);
			}
			else
			{
				throw new JsonException($"Unexpected token type: {reader.TokenType}");
			}

			foreach (var field in typeof(T).GetFields())
			{
				var attribute = field.GetCustomAttribute<DescriptionAttribute>();
				if ((attribute != null && attribute.Description == value) || field.Name == value)
				{
					return (T)field.GetValue(null);
				}
			}

			throw new JsonException($"Unable to convert \"{value}\" to {typeof(T)}.");
		}

		public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
		{
			var field = typeof(T).GetField(value.ToString());
			var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
			writer.WriteStringValue(attribute?.Description ?? value.ToString());
		}
	}
}

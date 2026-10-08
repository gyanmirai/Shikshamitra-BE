using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;

/// <summary>
/// Payment-term schedule fields are calendar dates. Keep the date written by the
/// client instead of shifting midnight to the server's time zone.
/// </summary>
public sealed class PaymentTermCalendarDateJsonConverter : JsonConverter<DateTime>
{
	public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		return ReadCalendarDate(ref reader);
	}

	public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value);
	}

	internal static DateTime ReadCalendarDate(ref Utf8JsonReader reader) =>
		DateTime.SpecifyKind(reader.GetDateTimeOffset().Date, DateTimeKind.Utc);
}

public sealed class PaymentTermNullableCalendarDateJsonConverter : JsonConverter<DateTime?>
{
	public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
		reader.TokenType == JsonTokenType.Null
			? null
			: PaymentTermCalendarDateJsonConverter.ReadCalendarDate(ref reader);

	public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
	{
		if (value.HasValue)
		{
			writer.WriteStringValue(value.Value);
		}
		else
		{
			writer.WriteNullValue();
		}
	}
}

public sealed class PaymentTermCalendarDateListJsonConverter : JsonConverter<List<DateTime>>
{
	public override List<DateTime> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartArray)
		{
			throw new JsonException("Invoice dates must be an array of calendar dates.");
		}
		var dates = new List<DateTime>();
		while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
		{
			dates.Add(PaymentTermCalendarDateJsonConverter.ReadCalendarDate(ref reader));
		}
		return dates;
	}

	public override void Write(Utf8JsonWriter writer, List<DateTime> value, JsonSerializerOptions options)
	{
		writer.WriteStartArray();
		foreach (var date in value)
		{
			writer.WriteStringValue(date);
		}
		writer.WriteEndArray();
	}
}

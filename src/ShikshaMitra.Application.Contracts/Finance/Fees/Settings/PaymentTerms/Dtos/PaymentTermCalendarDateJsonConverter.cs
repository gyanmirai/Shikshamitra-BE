using System;
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

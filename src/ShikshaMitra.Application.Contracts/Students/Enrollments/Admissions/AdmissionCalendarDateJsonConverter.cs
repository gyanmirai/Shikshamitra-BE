using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>
/// Admission window boundaries are calendar dates, not instants. Preserve the
/// client's written date before DateTime binding can convert it to server local time.
/// UTC midnight is the storage/wire representation, not a timezone conversion.
/// </summary>
public sealed class AdmissionCalendarDateJsonConverter : JsonConverter<DateTime>
{
	public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
		DateTime.SpecifyKind(reader.GetDateTimeOffset().Date, DateTimeKind.Utc);

	public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
		writer.WriteStringValue(DateTime.SpecifyKind(value.Date, DateTimeKind.Utc));
}

public sealed class AdmissionNullableCalendarDateJsonConverter : JsonConverter<DateTime?>
{
	public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
		reader.TokenType == JsonTokenType.Null
			? null
			: DateTime.SpecifyKind(reader.GetDateTimeOffset().Date, DateTimeKind.Utc);

	public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
	{
		if (value.HasValue)
			writer.WriteStringValue(DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc));
		else
			writer.WriteNullValue();
	}
}

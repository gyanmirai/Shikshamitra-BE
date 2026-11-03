using System;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Attendance;

public class AttendanceSummaryQueryCompatibility_Tests
{
	[Fact]
	public void Omitted_daily_summary_query_setting_preserves_durable_legacy_correction_json()
	{
		// Existing correction receipts compare this serialized input byte-for-byte.
		const string legacy = "{\"StudentId\":\"00000000-0000-0000-0000-000000000000\",\"Status\":1,\"Late\":false,\"HalfDay\":false,\"Remarks\":null,\"Reason\":\"Correction\",\"Revision\":\"1\",\"OperationId\":\"00000000-0000-0000-0000-000000000000\",\"AcademicYearId\":\"00000000-0000-0000-0000-000000000000\",\"StandardId\":\"00000000-0000-0000-0000-000000000000\",\"DivisionId\":\"00000000-0000-0000-0000-000000000000\",\"Date\":\"2026-09-01T00:00:00\"}";
		var input = new AttendanceCorrectionInput { Status = AttendanceStatus.Present, Reason = "Correction", Revision = "1", Date = new DateTime(2026, 9, 1) };
		JsonSerializer.Serialize(input).ShouldBe(legacy);
		var withNullSetting = legacy[..^1] + ",\"IncludeCumulativeSummary\":null}";
		JsonSerializer.Serialize(JsonSerializer.Deserialize<AttendanceCorrectionInput>(withNullSetting)).ShouldBe(legacy);
	}
}

using System;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Options;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp.Json;
using Volo.Abp.Json.SystemTextJson.JsonConverters;
using Volo.Abp.Json.SystemTextJson.Modifiers;
using Volo.Abp.Timing;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCycleCalendarDate_Tests
{
	private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions(DateTimeKind.Utc);

	[Theory]
	[InlineData(DateTimeKind.Utc)]
	[InlineData(DateTimeKind.Local)]
	[InlineData(DateTimeKind.Unspecified)]
	public void Calendar_dates_bypass_abp_clock_normalization_without_changing_the_written_date(DateTimeKind clockKind)
	{
		var options = CreateJsonOptions(clockKind);
		var input = JsonSerializer.Deserialize<CreateUpdateAdmissionCycleDto>("""
			{"startDate":"2026-06-01T00:00:00+05:30","endDate":"2027-03-31T23:30:00-07:00"}
			""", options)!;
		input.StartDate.ShouldBe(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
		input.EndDate.ShouldBe(new DateTime(2027, 3, 31, 0, 0, 0, DateTimeKind.Utc));
		input.StartDate.Kind.ShouldBe(DateTimeKind.Utc);
		using var response = JsonDocument.Parse(JsonSerializer.Serialize(new AdmissionCycleDto
		{
			StartDate = input.StartDate, EndDate = input.EndDate
		}, options));
		response.RootElement.GetProperty("startDate").GetString().ShouldBe("2026-06-01T00:00:00Z");
		response.RootElement.GetProperty("endDate").GetString().ShouldBe("2027-03-31T00:00:00Z");
	}

	[Theory]
	[InlineData("2026-06-01T00:00:00+05:30", "2027-03-31T00:00:00+05:30")]
	[InlineData("2026-06-01T23:30:00-07:00", "2027-03-31T23:30:00-07:00")]
	[InlineData("2026-06-01T00:00:00+14:00", "2027-03-31T00:00:00+14:00")]
	[InlineData("2026-06-01T00:00:00Z", "2027-03-31T00:00:00Z")]
	[InlineData("2026-06-01T00:00:00", "2027-03-31T00:00:00")]
	[InlineData("2026-06-01", "2027-03-31")]
	public void All_window_commands_preserve_the_calendar_date_written_by_the_client(string start, string end)
	{
		var json = JsonSerializer.Serialize(new { startDate = start, endDate = end, newEndDate = end });
		var create = JsonSerializer.Deserialize<CreateUpdateAdmissionCycleDto>(json, JsonOptions)!;
		var clone = JsonSerializer.Deserialize<CloneAdmissionCycleDto>(json, JsonOptions)!;
		var window = JsonSerializer.Deserialize<UpdateAdmissionCycleWindowDto>(json, JsonOptions)!;
		var reopen = JsonSerializer.Deserialize<ReopenAdmissionCycleDto>(json, JsonOptions)!;

		foreach (var actual in new[] { create.StartDate, clone.StartDate, window.StartDate })
		{
			actual.ShouldBe(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
			actual.Kind.ShouldBe(DateTimeKind.Utc);
		}
		foreach (var actual in new[] { create.EndDate, clone.EndDate, window.EndDate, reopen.NewEndDate!.Value })
		{
			actual.ShouldBe(new DateTime(2027, 3, 31, 0, 0, 0, DateTimeKind.Utc));
			actual.Kind.ShouldBe(DateTimeKind.Utc);
		}

		var cycle = new AdmissionCycle(Guid.NewGuid(), null, "Calendar date", "CALENDAR", Guid.NewGuid(),
			create.StartDate, create.EndDate);
		cycle.Publish(Guid.NewGuid(), DateTime.UtcNow);
		cycle.ReviseWindow(window.StartDate, window.EndDate);
		var response = JsonSerializer.Serialize(new AdmissionCycleDto { StartDate = cycle.StartDate, EndDate = cycle.EndDate }, JsonOptions);
		using var document = JsonDocument.Parse(response);
		document.RootElement.GetProperty("startDate").GetString().ShouldBe("2026-06-01T00:00:00Z");
		document.RootElement.GetProperty("endDate").GetString().ShouldBe("2027-03-31T00:00:00Z");
	}

	[Fact]
	public void Reopen_without_a_new_date_stays_optional()
	{
		JsonSerializer.Deserialize<ReopenAdmissionCycleDto>("{}", JsonOptions)!.NewEndDate.ShouldBeNull();
		JsonSerializer.Deserialize<ReopenAdmissionCycleDto>("{\"newEndDate\":null}", JsonOptions)!.NewEndDate.ShouldBeNull();
	}

	[Theory]
	[InlineData("not-a-date")]
	[InlineData("2026-02-30T00:00:00+05:30")]
	public void Invalid_calendar_dates_are_rejected(string date)
	{
		Should.Throw<JsonException>(() => JsonSerializer.Deserialize<CreateUpdateAdmissionCycleDto>(
			JsonSerializer.Serialize(new { startDate = date }), JsonOptions));
	}

	[Fact]
	public void Scheduled_publish_and_close_keep_their_precise_utc_instants()
	{
		const string json = """
			{"publishAt":"2026-06-01T00:15:22+05:30","closeAt":"2027-03-31T23:45:33+05:30"}
			""";
		var schedule = JsonSerializer.Deserialize<ScheduleAdmissionCycleDto>(json, JsonOptions)!;
		var cycle = new AdmissionCycle(Guid.NewGuid(), null, "Scheduled", "SCHEDULED", Guid.NewGuid(),
			new DateTime(2026, 6, 1), new DateTime(2027, 3, 31));
		cycle.SetSchedule(schedule.PublishAt, schedule.CloseAt);
		cycle.ScheduledPublishAt.ShouldBe(new DateTime(2026, 5, 31, 18, 45, 22, DateTimeKind.Utc));
		cycle.ScheduledCloseAt.ShouldBe(new DateTime(2027, 3, 31, 18, 15, 33, DateTimeKind.Utc));
		cycle.Publish(Guid.NewGuid(), cycle.ScheduledPublishAt!.Value);
		cycle.ScheduledPublishAt.ShouldBeNull();
		cycle.ScheduledCloseAt.ShouldNotBeNull();
		cycle.Close(Guid.NewGuid(), cycle.ScheduledCloseAt.Value);
		cycle.ScheduledCloseAt.ShouldBeNull();
	}

	private static JsonSerializerOptions CreateJsonOptions(DateTimeKind clockKind)
	{
		var clock = new Clock(Options.Create(new AbpClockOptions { Kind = clockKind }));
		var settings = Options.Create(new AbpJsonOptions());
		var modifier = new AbpDateTimeConverterModifier(new AbpDateTimeConverter(clock, settings),
			new AbpNullableDateTimeConverter(clock, settings));
		var resolver = new DefaultJsonTypeInfoResolver();
		resolver.Modifiers.Add(modifier.CreateModifyAction());
		return new JsonSerializerOptions(JsonSerializerDefaults.Web) { TypeInfoResolver = resolver };
	}
}

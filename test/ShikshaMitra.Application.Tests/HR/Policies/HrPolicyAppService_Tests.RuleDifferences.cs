using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Policies;

public partial class HrPolicyAppService_Tests
{
	[Theory]
	[InlineData(0, 6, "Sunday", "Saturday")]
	[InlineData(-1, 0, "None", "Sunday")]
	[InlineData(6, -1, "Saturday", "None")]
	public async Task RuleDifferencePreview_Weekly_Off_Changes_Additions_And_Removals_Are_Readable(
		int fromDay, int toDay, string fromDisplay, string toDisplay)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var reader = GetRequiredService<HrPolicyVersionReadService>();
			var current = new { customWeeklyOffs = fromDay < 0 ? Array.Empty<int>() : new[] { fromDay } };
			var proposed = new { customWeeklyOffs = toDay < 0 ? Array.Empty<int>() : new[] { toDay } };
			var difference = (await reader.CompareProposedRulesAsync(current, proposed)).ShouldHaveSingleItem();
			difference.FieldKey.ShouldBe("customWeeklyOffs");
			difference.FromDisplayValue.ShouldBe(fromDisplay);
			difference.ToDisplayValue.ShouldBe(toDisplay);
			difference.FromValue.ShouldBe(fromDisplay);
			difference.ToValue.ShouldBe(toDisplay);
			difference.FromRawValue.ShouldBe(JsonSerializer.Serialize(current.customWeeklyOffs));
			difference.ToRawValue.ShouldBe(JsonSerializer.Serialize(proposed.customWeeklyOffs));
		});
	}

	[Fact]
	public async Task RuleDifferencePreview_Existing_Immutable_Version_Comparison_Retains_Typed_Weekdays()
	{
		var input = new CreateUpdateWorkCalendarPolicyDto
		{
			AcademicYearId = await CreateAcademicYearAsync(),
			Name = $"Synthetic weekly history {Guid.NewGuid():N}",
			Status = "draft",
			WeeklyOffPattern = "custom",
			CustomWeeklyOffs = [0]
		};
		var created = await _workCalendars.CreateAsync(input);
		input.CustomWeeklyOffs = [6];
		var updated = await _workCalendars.UpdateAsync(created.Id, input);
		var versions = await _policyVersions.GetListAsync("work_calendar", created.Id, new() { MaxResultCount = 10 });
		var first = versions.Items.Single(x => x.VersionNumber == created.Version);
		var latest = versions.Items.Single(x => x.VersionNumber == updated.Version);
		var comparison = await _policyVersions.CompareAsync("work_calendar", created.Id, first.Id, latest.Id);
		var difference = comparison.Differences.Single(x => x.FieldKey == "customWeeklyOffs");
		difference.FromRawValue.ShouldBe("[0]");
		difference.ToRawValue.ShouldBe("[6]");
		difference.FromDisplayValue.ShouldBe("Sunday");
		difference.ToDisplayValue.ShouldBe("Saturday");
		var after = await _policyVersions.GetListAsync("work_calendar", created.Id, new() { MaxResultCount = 10 });
		after.Items.Single(x => x.Id == first.Id).SnapshotJson.ShouldBe(first.SnapshotJson);
		after.Items.Single(x => x.Id == latest.Id).SnapshotJson.ShouldBe(latest.SnapshotJson);
	}

	[Fact]
	public async Task RuleDifferencePreview_Weekly_Off_Order_Alone_Is_Not_A_Change()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var reader = GetRequiredService<HrPolicyVersionReadService>();
			(await reader.CompareProposedRulesAsync(new { customWeeklyOffs = new[] { 6, 0 } }, new { customWeeklyOffs = new[] { 0, 6 } })).ShouldBeEmpty();
		});
	}

	[Fact]
	public async Task RuleDifferencePreview_WorkCalendar_Impact_Exposes_Changed_Weekly_Offs()
	{
		var context = await CreatePolicyContextAsync();
		var proposed = JsonSerializer.Deserialize<CreateUpdateWorkCalendarPolicyDto>(JsonSerializer.Serialize(context.WorkCalendar))!;
		proposed.WeeklyOffPattern = "custom";
		proposed.CustomWeeklyOffs = [6];
		var preview = await _policySafety.PreviewPolicyImpactAsync(new()
		{
			PolicyType = "work_calendar",
			PolicyId = context.WorkCalendar.Id,
			AcademicYearId = context.AcademicYearId,
			Action = "update",
			WorkCalendarPolicy = proposed
		});
		preview.RuleDifferencesStatus.ShouldBe("calculated");
		preview.RuleDifferences.Single(x => x.FieldKey == "customWeeklyOffs").ToDisplayValue.ShouldBe("Saturday");
		(await _workCalendars.GetAsync(context.WorkCalendar.Id)).CustomWeeklyOffs.ShouldBe(context.WorkCalendar.CustomWeeklyOffs);
	}

	[Fact]
	public async Task RuleDifferencePreview_Sibling_Collections_Keep_Operational_Changes_Readable()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var reader = GetRequiredService<HrPolicyVersionReadService>();
			var current = new
			{
				manualMonthDays = new[] { new { year = 2026, month = 5, manualDays = 30 } },
				holidayOverrides = new[] { new { date = "2026-05-01", holidayName = "Synthetic holiday", includedInPayroll = true } }
			};
			var proposed = new
			{
				manualMonthDays = new[] { new { year = 2026, month = 5, manualDays = 31 } },
				holidayOverrides = new[] { new { date = "2026-05-01", holidayName = "Synthetic holiday", includedInPayroll = false } }
			};
			var differences = await reader.CompareProposedRulesAsync(current, proposed);
			differences.Count.ShouldBe(2);
			var month = differences.Single(x => x.FieldKey == "manualMonthDays");
			month.FromDisplayValue.ShouldBe("2026-5: 30 days");
			month.ToDisplayValue.ShouldBe("2026-5: 31 days");
			var holiday = differences.Single(x => x.FieldKey == "holidayOverrides");
			holiday.FromDisplayValue.ShouldContain("Included in payroll");
			holiday.ToDisplayValue.ShouldContain("Excluded from payroll");
		});
	}

	[Fact]
	public async Task RuleDifferencePreview_Shows_An_Actual_Threshold_Change_Without_Response_Metadata_Removals()
	{
		var context = await CreatePolicyContextAsync();
		var proposed = JsonSerializer.Deserialize<CreateUpdateAttendancePolicyDto>(JsonSerializer.Serialize(context.AttendancePolicy))!;
		proposed.FullDayThresholdPercentage = 80;
		var preview = await _policySafety.PreviewPolicyImpactAsync(new()
		{
			PolicyType = "attendance",
			PolicyId = context.AttendancePolicy.Id,
			AcademicYearId = context.AcademicYearId,
			Action = "update",
			AttendancePolicy = proposed
		});
		preview.RuleDifferencesStatus.ShouldBe("calculated");
		var difference = preview.RuleDifferences.ShouldHaveSingleItem();
		difference.FieldKey.ShouldBe("fullDayThresholdPercentage");
		difference.FromRawValue.ShouldBe("75");
		difference.ToRawValue.ShouldBe("80");
		difference.FieldLabel.ShouldNotBe("fullDayThresholdPercentage");
		(await _attendancePolicies.GetAsync(context.AttendancePolicy.Id)).FullDayThresholdPercentage.ShouldBe(75);
	}

	[Fact]
	public async Task RuleDifferencePreview_Does_Not_Treat_Omitted_Ids_Versions_Or_Audit_Metadata_As_Rule_Changes()
	{
		var context = await CreatePolicyContextAsync();
		var proposed = JsonSerializer.Deserialize<CreateUpdateAttendancePolicyDto>(JsonSerializer.Serialize(context.AttendancePolicy))!;
		var preview = await _policySafety.PreviewPolicyImpactAsync(new()
		{
			PolicyType = "attendance",
			PolicyId = context.AttendancePolicy.Id,
			AcademicYearId = context.AcademicYearId,
			Action = "update",
			AttendancePolicy = proposed
		});
		preview.RuleDifferencesStatus.ShouldBe("calculated");
		preview.RuleDifferences.ShouldBeEmpty();
	}

	[Fact]
	public async Task RuleDifferencePreview_Ignores_Holiday_Row_Ids_But_Shows_Operational_Values()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var reader = GetRequiredService<HrPolicyVersionReadService>();
			var current = new { id = Guid.NewGuid(), version = 4, holidays = new[] { new { id = Guid.NewGuid(), holidayName = "Synthetic holiday", date = "2026-05-01", fromDate = "2026-05-01", toDate = "2026-05-01", isIncluded = true, source = "manual" } } };
			var unchanged = new { holidays = new[] { new { id = Guid.NewGuid(), holidayName = "Synthetic holiday", date = "2026-05-01", fromDate = "2026-05-01", toDate = "2026-05-01", isIncluded = true, source = "manual" } } };
			(await reader.CompareProposedRulesAsync(current, unchanged)).ShouldBeEmpty();
			var changed = new { holidays = new[] { new { id = Guid.NewGuid(), holidayName = "Synthetic holiday", date = "2026-05-01", fromDate = "2026-05-01", toDate = "2026-05-01", isIncluded = false, source = "manual" } } };
			var difference = (await reader.CompareProposedRulesAsync(current, changed)).ShouldHaveSingleItem();
			difference.FieldKey.ShouldBe("holidays");
			difference.FromDisplayValue.ShouldContain("Synthetic holiday | 2026-05-01 to 2026-05-01 | Included");
			difference.ToDisplayValue.ShouldContain("Synthetic holiday | 2026-05-01 to 2026-05-01 | Excluded");
		});
	}
}
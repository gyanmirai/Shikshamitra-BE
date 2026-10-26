using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Policies;

public partial class HrPolicyAppService_Tests
{
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
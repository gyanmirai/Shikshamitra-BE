using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentOnboardingAppService
{
	private async Task PrepareForLockedGroupAsync(StudentOnboardingCase item)
	{
		var grLocked = await IsDivisionIdentifierGroupLockedAsync(item.DivisionId, item.AcademicYearId, "gr");
		var rollLocked = await IsDivisionIdentifierGroupLockedAsync(item.DivisionId, item.AcademicYearId, "roll");
		if (!grLocked && !rollLocked) return;
		var student = await studentRepository.GetAsync(item.StudentId);
		if (grLocked && item.IdentifierAllocationId == null && string.IsNullOrWhiteSpace(student.GrNumber) && !student.GrNo.HasValue)
		{
			var config = await configurationManager.GetAsync(item.AcademicYearId);
			var series = config == null ? null : await GetConfiguredSeriesAsync(config, item, true);
			if (series == null) throw new BusinessException(StudentOnboardingErrorCodes.IdentifierSeriesUnavailable);
			long sequence; string value;
			do { (sequence, value) = series.Allocate(Clock.Now); } while (await GrValueExistsAsync(value, student.Id, null));
			await seriesRepository.UpdateAsync(series, false);
			var allocation = new StudentIdentifierAllocation(GuidGenerator.Create(), CurrentTenant.Id, item.Id, student.Id,
				series.Id, StudentIdentifierType.GrNumber, sequence, value, Clock.Now);
			await allocationRepository.InsertAsync(allocation, false); item.RecordGrIdentifier(allocation.Id, Clock.Now);
			item.SetIdentifierLocked("gr", true); student.SetIdentifierLocked("gr", true);
			await AddEventAsync(item.Id, "GrNumberPrepared", $"New student received next GR sequence {value} in the locked identifier group.");
		}
		if (rollLocked && !item.RollNumber.HasValue)
		{
			var reader = LazyServiceProvider.LazyGetRequiredService<StudentOnboardingRollRuleReader>();
			var original = await reader.CaptureForCaseAsync(item);
			var rule = JsonNode.Parse(original) as JsonObject ?? new JsonObject();
			rule["assignmentMode"] = "automatic"; rule["startingNumber"] = 1; rule["numberingScope"] = "class-division";
			rule["preserveExisting"] = true; rule["midYearHandling"] = "next-available"; rule["assignmentTiming"] = "after-division";
			var proposal = await LazyServiceProvider.LazyGetRequiredService<StudentOnboardingRollNumberAllocator>().ProposeAsync(
				item.StudentId, item.AcademicYearId, item.IntendedStandardId, item.DivisionId!.Value, null, "automatic", rule.ToJsonString(), "");
			item.RecordIdentifier(item.IdentifierAllocationId, proposal.RollNumber, Clock.Now);
			item.SetIdentifierLocked("roll", true); student.SetIdentifierLocked("roll", true);
			await AddEventAsync(item.Id, "RollNumberPrepared", $"New student received next division roll {proposal.RollNumber} in the locked identifier group.");
		}
        if (grLocked) { item.SetIdentifierLocked("gr", true); student.SetIdentifierLocked("gr", true); }
        if (rollLocked) { item.SetIdentifierLocked("roll", true); student.SetIdentifierLocked("roll", true); }
		await studentRepository.UpdateAsync(student, false);
	}

	private async Task RefreshIdentifierStepAsync(StudentOnboardingCase item)
	{
		var student = await studentRepository.GetAsync(item.StudentId);
		if (item.RollNumber.HasValue && (item.IdentifierAllocationId.HasValue || !string.IsNullOrWhiteSpace(student.GrNumber) || student.GrNo.HasValue))
			await CompleteSystemStepAsync(item.Id, StudentOnboardingStepKeys.IdentifiersAndRoll, "GR and Roll numbers prepared for activation.", Clock.Now);
		else await ResetSystemStepAsync(item.Id, StudentOnboardingStepKeys.IdentifiersAndRoll, "Prepare the pending GR or Roll number.");
	}
}

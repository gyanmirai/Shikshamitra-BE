using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

/// <summary>Onboarding owns roll starting number, class/division scope, and its approval/reason requirements.</summary>
[RemoteService(false)]
public class StudentOnboardingRollRuleReader(OnboardingConfigurationManager configuration, IRepository<Standard, Guid> standards,
	IRepository<StudentOnboardingCase, Guid> cases, IRepository<StudentEnrollment, Guid> enrollments,
	IRepository<StudentRecordRenewalCase, Guid> renewals,
	IRepository<StudentRecordTransition, Guid> transitions,
	IRepository<Division, Guid> divisions, IRepository<StudentRecordChange, Guid> recordChanges) : ApplicationService, ITransientDependency
{
	private static string SequenceKey(Guid yearId) => $"RollHighWater:{yearId:N}";
	/// <summary>The cursor survives exits, cancelled reservations and manual corrections.</summary>
	public async Task<int> LastIssuedAsync(Guid yearId, Guid standardId, Guid divisionId)
	{
		var division = await divisions.GetAsync(x => x.Id == divisionId && x.TenantId == CurrentTenant.Id);
		if (division.StandardId != standardId) throw new BusinessException(StudentOnboardingErrorCodes.PlacementInvalid);
		var current = await GetReservedNumbersAsync(Guid.Empty, yearId, standardId, divisionId, "null");
		if (division.ExtraProperties.TryGetValue(SequenceKey(yearId), out var saved) && int.TryParse(saved?.ToString(), out var cursor))
			return Math.Max(cursor, current.DefaultIfEmpty(0).Max());
		// Bootstrap from retained records, including inactive/cancelled history, not just today's roster.
		var historical = (await enrollments.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && x.DivisionId == divisionId)).Select(x => x.RollNumber).ToList();
		historical.AddRange((await cases.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && x.DivisionId == divisionId)).Select(x => x.RollNumber));
		historical.AddRange((await renewals.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && x.DivisionId == divisionId)).Select(x => x.RollNumber));
		// A moved student or a same-day correction can no longer carry the old number
		// in its current projection. Retained dated placements and change before-images
		// are part of the upgrade baseline, without changing any historical records.
		var divisionText = divisionId.ToString("D");
		var placements = await AsyncExecuter.ToListAsync((await enrollments.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && x.AttendancePlacementHistoryJson.Contains(divisionText)).Select(x => x.AttendancePlacementHistoryJson));
		foreach (var json in placements)
		{
			using var document = JsonDocument.Parse(json);
			foreach (var row in document.RootElement.EnumerateArray()) AddHistory(row, "DivisionId", "RollNumber");
		}
		var corrections = await AsyncExecuter.ToListAsync((await recordChanges.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && x.Section == "placement" && x.Status == "Applied" && x.BeforeJson.Contains(divisionText)).Select(x => x.BeforeJson));
		foreach (var json in corrections) { using var document = JsonDocument.Parse(json); AddHistory(document.RootElement, "divisionId", "rollNumber"); }
		void AddHistory(JsonElement row, string divisionKey, string numberKey)
		{
			if (row.TryGetProperty(divisionKey, out var divisionValue) && divisionValue.ValueKind == JsonValueKind.String && divisionValue.TryGetGuid(out var recordedDivision) && recordedDivision == divisionId &&
				row.TryGetProperty(numberKey, out var numberValue) && numberValue.ValueKind == JsonValueKind.Number && numberValue.TryGetInt32(out var recordedNumber)) historical.Add(recordedNumber);
		}
		foreach (var transition in await transitions.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.TargetAcademicYearId == yearId && x.TargetDivisionId == divisionId))
		{
			using var document = JsonDocument.Parse(transition.InputJson);
			if (document.RootElement.TryGetProperty("RollNumber", out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) historical.Add(number);
		}
		return Math.Max(historical.Where(x => x.HasValue).Select(x => x!.Value).DefaultIfEmpty(0).Max(), current.DefaultIfEmpty(0).Max());
	}
	public async Task RecordIssuedAsync(Guid yearId, Guid standardId, Guid divisionId, int number, bool reset = false)
	{
		if (number < 0) throw new BusinessException(StudentOnboardingErrorCodes.RollNumberInvalid);
		var previous = reset ? 0 : await LastIssuedAsync(yearId, standardId, divisionId);
		var division = await divisions.GetAsync(x => x.Id == divisionId && x.TenantId == CurrentTenant.Id);
		if (division.StandardId != standardId) throw new BusinessException(StudentOnboardingErrorCodes.PlacementInvalid);
		division.ExtraProperties[SequenceKey(yearId)] = Math.Max(previous, number);
		// Every reservation write participates in optimistic scope concurrency, even below the high-water mark.
		division.ExtraProperties[SequenceKey(yearId) + ":revision"] = Guid.NewGuid().ToString("N");
		await divisions.UpdateAsync(division, false);
	}
	public async Task<string> GetAssignmentModeAsync(Guid yearId)
	{
		var settings = await configuration.GetAsync(yearId);
		return settings == null ? "manual" : OnboardingRuleConfiguration.Text(settings.Root.GetProperty("rollNumber"), "assignmentMode", "both");
	}
	public async Task<string> CaptureAsync(Guid studentId, Guid yearId, Guid standardId, Guid divisionId)
	{
		var settings = await configuration.GetAsync(yearId); if (settings == null) return "null";
		var standard = await standards.GetAsync(x => x.Id == standardId && x.TenantId == CurrentTenant.Id, includeDetails: true);
		var stage = standard.EducationMediumStage; var medium = stage.EducationMedium;
		var previous = (await cases.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.Status == StudentOnboardingStatus.Completed)).OrderByDescending(x => x.CompletedAt).FirstOrDefault();
		var rule = settings.ResolveRule("rollNumber", medium.EducationBoardId, medium.Id, stage.Id, standard.Id, divisionId, previous?.GetCampusId(),
			ShikshaMitra.Students.StudentEnrollments.Admissions.AdmissionCycleWindowEvaluator.GetInstitutionDate(Clock.Now));
		var frozen = rule.HasValue ? JsonNode.Parse(rule.Value.GetRawText())!.AsObject() : new JsonObject();
		frozen["numberingScope"] = "class-division";
		frozen["startingNumber"] = 1;
		frozen["assignmentMode"] = OnboardingRuleConfiguration.Text(settings.Root.GetProperty("rollNumber"), "assignmentMode", "both");
		return frozen.ToJsonString();
	}
	public async Task<string> CaptureForCaseAsync(StudentOnboardingCase item)
	{
		var settings = await configuration.GetAsync(item.AcademicYearId); if (settings == null) return "null";
		var rule = settings.ResolveRule("rollNumber", item, ShikshaMitra.Students.StudentEnrollments.Admissions.AdmissionCycleWindowEvaluator.GetInstitutionDate(Clock.Now));
		var frozen = rule.HasValue ? JsonNode.Parse(rule.Value.GetRawText())!.AsObject() : new JsonObject();
		frozen["numberingScope"] = "class-division";
		frozen["startingNumber"] = 1;
		frozen["assignmentMode"] = OnboardingRuleConfiguration.Text(settings.Root.GetProperty("rollNumber"), "assignmentMode", "both");
		// The owning identifier command checks its actual before/after change.
		frozen["requireChangeReason"] = false;
		if (item.IsIdentifierLocked("roll") && !item.RollNumber.HasValue) frozen["assignmentMode"] = "automatic";
		return frozen.ToJsonString();
	}
	public static JsonElement? Parse(string json)
	{
		using var document = JsonDocument.Parse(json); return document.RootElement.ValueKind == JsonValueKind.Null ? null : document.RootElement.Clone();
	}
	public async Task ValidateAsync(Guid studentId, Guid yearId, Guid standardId, Guid divisionId, int? roll, string reason, string ruleJson)
	{
		if (!roll.HasValue || roll <= 0) throw new BusinessException(StudentOnboardingErrorCodes.RollNumberInvalid);
		var rule = Parse(ruleJson);
		if (rule.HasValue && roll < OnboardingRuleConfiguration.Number(rule.Value, "startingNumber", 1))
			throw new BusinessException(StudentOnboardingErrorCodes.RollNumberInvalid).WithData("MinimumRoll", OnboardingRuleConfiguration.Number(rule.Value, "startingNumber", 1));
		if (rule.HasValue && OnboardingRuleConfiguration.Boolean(rule.Value, "requireChangeReason") && string.IsNullOrWhiteSpace(reason)) throw new BusinessException(StudentOnboardingErrorCodes.WaiverReasonRequired);
		var classScope = rule.HasValue && OnboardingRuleConfiguration.Text(rule.Value, "numberingScope") == "class";
		if (await enrollments.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId != studentId && x.AcademicYearId == yearId && x.IsActive && x.RollNumber == roll && (classScope ? x.StandardId == standardId : x.DivisionId == divisionId)) ||
			await cases.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId != studentId && x.AcademicYearId == yearId && x.RollNumber == roll && x.Status != StudentOnboardingStatus.Completed && x.Status != StudentOnboardingStatus.Cancelled && (classScope ? x.IntendedStandardId == standardId : x.DivisionId == divisionId)) ||
			await renewals.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId != studentId && x.AcademicYearId == yearId && x.RollNumber == roll && (x.Status == "PendingChecklist" || x.Status == "Provisioning") && (classScope ? x.StandardId == standardId : x.DivisionId == divisionId)))
			throw new BusinessException(StudentOnboardingErrorCodes.RollNumberDuplicate);
		if ((await GetReservedNumbersAsync(studentId, yearId, standardId, divisionId, ruleJson)).Contains(roll.Value))
			throw new BusinessException(StudentOnboardingErrorCodes.RollNumberDuplicate);
	}
	public async Task<List<int>> GetReservedNumbersAsync(Guid studentId, Guid yearId, Guid standardId, Guid divisionId, string ruleJson)
	{
		var rule = Parse(ruleJson); var classScope = rule.HasValue && OnboardingRuleConfiguration.Text(rule.Value, "numberingScope") == "class";
		var numbers = (await enrollments.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId != studentId && x.AcademicYearId == yearId && x.IsActive && (classScope ? x.StandardId == standardId : x.DivisionId == divisionId))).Select(x => x.RollNumber).ToList();
		numbers.AddRange((await cases.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId != studentId && x.AcademicYearId == yearId && x.Status != StudentOnboardingStatus.Completed && x.Status != StudentOnboardingStatus.Cancelled && (classScope ? x.IntendedStandardId == standardId : x.DivisionId == divisionId))).Select(x => x.RollNumber));
		numbers.AddRange((await renewals.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId != studentId && x.AcademicYearId == yearId && (x.Status == "PendingChecklist" || x.Status == "Provisioning") && (classScope ? x.StandardId == standardId : x.DivisionId == divisionId))).Select(x => x.RollNumber));
		foreach (var pending in await transitions.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId != studentId && x.TargetAcademicYearId == yearId &&
			(x.Status == "Submitted" || x.Status == "Approved" || x.Status == "Scheduled" || x.Status == "AwaitingRenewal" || x.Status == "Exception") && (classScope ? x.TargetStandardId == standardId : x.TargetDivisionId == divisionId)))
		{
			using var document = JsonDocument.Parse(pending.InputJson);
			if (document.RootElement.TryGetProperty("RollNumber", out var number) && number.ValueKind == JsonValueKind.Number && number.TryGetInt32(out var value)) numbers.Add(value);
		}
		return numbers.Where(x => x.HasValue && x.Value > 0).Select(x => x!.Value).Distinct().ToList();
	}
}

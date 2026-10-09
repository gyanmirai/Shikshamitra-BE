using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using ShikshaMitra.TenantSettings.Standards;
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
	IRepository<StudentRecordTransition, Guid> transitions) : ApplicationService, ITransientDependency
{
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

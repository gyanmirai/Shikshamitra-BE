using System;

namespace ShikshaMitra.HR.Policies;

public static class HrPolicyVersionProjection
{
	public static HrPolicyVersionDto ToDto(HrPolicyVersion entity) => new()
	{
		Id = entity.Id,
		PolicyType = entity.PolicyType,
		PolicyId = entity.PolicyId,
		AcademicYearId = entity.AcademicYearId,
		VersionNumber = entity.VersionNumber,
		Name = entity.Name,
		Status = entity.Status,
		EffectiveFrom = entity.EffectiveFrom,
		EffectiveTo = entity.EffectiveTo,
		ChangeAction = entity.ChangeAction,
		ChangeReason = entity.ChangeReason,
		CreationTime = entity.CreationTime,
		CreatorId = entity.CreatorId,
		Summary = BuildVersionSummary(entity),
		SnapshotJson = entity.SnapshotJson
	};

	public static string BuildVersionSummary(HrPolicyVersion entity) =>
		$"{HrPolicyVersionDiffBuilder.ToDisplayText(entity.ChangeAction)} v{entity.VersionNumber}";

	public static string NormalizePolicyType(string value) => value.Trim().ToLowerInvariant().Replace("-", "_") switch
	{
		"workcalendar" or "work_calendar" or "work-calendars" or "work_calendar_policies" => "work_calendar",
		"payroll" or "payroll_policy" => "payroll",
		"attendance" or "attendance_policy" => "attendance",
		_ => throw new Volo.Abp.BusinessException(HRErrorCodes.HrPolicyInvalidType)
	};

	public static string ToAuditPolicyEntityType(string normalizedPolicyType) => normalizedPolicyType switch
	{
		"work_calendar" => "work_calendar_policy",
		"payroll" => "payroll_policy",
		"attendance" => "attendance_policy",
		_ => "policy_version"
	};

	public static HrPolicyImpactPreviewRequestDto ToRestorePreviewRequest(
		string policyType,
		Guid policyId,
		HrPolicyVersion version) => new()
	{
		PolicyType = NormalizePolicyType(policyType),
		PolicyId = policyId,
		AcademicYearId = version.AcademicYearId,
		Action = "restore-draft"
	};

	public static string NormalizeAuditAction(string action) => action.Trim().ToLowerInvariant().Replace("-", "_") switch
	{
		"restore_draft" => "restored_draft",
		var value => value
	};
}

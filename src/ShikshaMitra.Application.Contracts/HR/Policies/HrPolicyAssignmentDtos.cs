using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyAssignmentDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public string TargetType { get; set; } = "employee";
	public string TargetKey { get; set; } = string.Empty;
	public string? TargetDisplayName { get; set; }
	public List<string> TargetOrganizationNames { get; set; } = [];
	public Guid AttendancePolicyId { get; set; }
	public Guid PayrollPolicyId { get; set; }
	public Guid? LeavePolicyId { get; set; }
	public string? PayrollPolicyName { get; set; }
	public string? AttendancePolicyName { get; set; }
	public string? LeavePolicyName { get; set; }
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public bool IsActive { get; set; }
	public int Priority { get; set; }
	public string? AssignmentReason { get; set; }
	public DateTime CreationTime { get; set; }
	public Guid? CreatorId { get; set; }
	public DateTime? LastModificationTime { get; set; }
	public Guid? LastModifierId { get; set; }
}

public class CreateUpdateHrPolicyAssignmentDto
{
	public Guid AcademicYearId { get; set; }
	public string TargetType { get; set; } = "employee";
	public string TargetKey { get; set; } = string.Empty;
	public string? TargetDisplayName { get; set; }
	public Guid AttendancePolicyId { get; set; }
	public Guid PayrollPolicyId { get; set; }
	public Guid LeavePolicyId { get; set; }
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public bool IsActive { get; set; } = true;
	public int Priority { get; set; }
	public string? AssignmentReason { get; set; }
	public string? ConfirmationToken { get; set; }
}

public class UpsertEmployeePolicyOverrideDto
{
	public Guid EmployeeId { get; set; }
	public Guid AcademicYearId { get; set; }
	public Guid? AssignmentId { get; set; }
	public Guid AttendancePolicyId { get; set; }
	public Guid PayrollPolicyId { get; set; }
	public Guid LeavePolicyId { get; set; }
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public string? AssignmentReason { get; set; }
	public string? ConfirmationToken { get; set; }
}

public class DisableEmployeePolicyOverrideDto
{
	public Guid EmployeeId { get; set; }
	public Guid AcademicYearId { get; set; }
	public Guid? AssignmentId { get; set; }
	public DateTime EffectiveDate { get; set; }
	public string? AssignmentReason { get; set; }
	public string? ConfirmationToken { get; set; }
}

public class EffectiveEmployeePoliciesDto
{
	public Guid EmployeeId { get; set; }
	public Guid AcademicYearId { get; set; }
	public DateTime Date { get; set; }
	public HrPolicyAssignmentDto? Assignment { get; set; }
	public string PolicySource { get; set; } = "default";
	public Guid? MatchedAssignmentId { get; set; }
	public string? MatchedTargetType { get; set; }
	public string? MatchedTargetKey { get; set; }
	public string? MatchedTargetDisplayName { get; set; }
	public int? MatchedPriority { get; set; }
	public AttendancePolicyDto AttendancePolicy { get; set; } = new();
	public PayrollPolicyDto PayrollPolicy { get; set; } = new();
	public EffectiveLeavePolicyDto? LeavePolicy { get; set; }
	public WorkCalendarPolicyDto? AttendanceWorkCalendarPolicy { get; set; }
	public WorkCalendarPolicyDto? PayrollWorkCalendarPolicy { get; set; }
	public List<EffectivePolicyResolutionStepDto> ResolutionSteps { get; set; } = [];
	public string? ResolutionSummary { get; set; }
	public string? RiskSummary { get; set; }
	public string? RecommendedAction { get; set; }
}

public class EffectivePolicyResolutionStepDto
{
	public string Source { get; set; } = string.Empty;
	public string TargetType { get; set; } = string.Empty;
	public string? TargetKey { get; set; }
	public string? TargetDisplayName { get; set; }
	public Guid? AssignmentId { get; set; }
	public int? Priority { get; set; }
	public string Status { get; set; } = "skipped";
	public string SkipReason { get; set; } = "no_assignment";
	public Guid? PayrollPolicyId { get; set; }
	public Guid? AttendancePolicyId { get; set; }
	public Guid? LeavePolicyId { get; set; }
}

public class EffectiveLeavePolicyDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public List<EffectiveLeavePolicyRuleDto> Rules { get; set; } = [];
	public List<string> AllowedPaidTypes { get; set; } = [];
}

public class EffectiveLeavePolicyRuleDto
{
	public Guid RuleId { get; set; }
	public string PaidType { get; set; } = string.Empty;
	public string EntitlementMode { get; set; } = "Limited";
	public int? LeaveCount { get; set; }
	public bool IsUnlimited { get; set; }
	public bool RequiresApproval { get; set; }
	public int MaxDaysPerRequest { get; set; }
	public bool AllowHalfDay { get; set; }
	public int? AttachmentRequiredAfterDays { get; set; }
	public bool AllowNegativeBalance { get; set; }
}

public class PolicyAssignmentPreviewRequestDto
{
	public Guid AcademicYearId { get; set; }
	public DateTime Date { get; set; }
	public Guid? EmployeeId { get; set; }
	public Guid? GroupId { get; set; }
}

public class PolicyAssignmentResolutionPreviewRequestDto
{
	public Guid? AssignmentId { get; set; }
	public CreateUpdateHrPolicyAssignmentDto Assignment { get; set; } = new();
	public DateTime Date { get; set; }
	public Guid? SampleEmployeeId { get; set; }
}

public class PolicyAssignmentResolutionPreviewDto
{
	public EffectiveEmployeePoliciesDto Current { get; set; } = new();
	public EffectiveEmployeePoliciesDto? Proposed { get; set; }
	public bool IsComplete { get; set; }
	public string Scope { get; set; } = string.Empty;
	public string? UnavailableReason { get; set; }
}
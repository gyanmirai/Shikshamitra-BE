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
	public Guid AttendancePolicyId { get; set; }
	public Guid PayrollPolicyId { get; set; }
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public string? AssignmentReason { get; set; }
	public string? ConfirmationToken { get; set; }
}

public class DisableEmployeePolicyOverrideDto
{
	public Guid EmployeeId { get; set; }
	public Guid AcademicYearId { get; set; }
	public DateTime EffectiveDate { get; set; }
	public string? AssignmentReason { get; set; }
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
	public WorkCalendarPolicyDto? AttendanceWorkCalendarPolicy { get; set; }
	public WorkCalendarPolicyDto? PayrollWorkCalendarPolicy { get; set; }
	public List<EffectivePolicyResolutionStepDto> ResolutionSteps { get; set; } = [];
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
}

public class PolicyAssignmentPreviewRequestDto
{
	public Guid AcademicYearId { get; set; }
	public DateTime Date { get; set; }
	public Guid? EmployeeId { get; set; }
	public Guid? GroupId { get; set; }
}

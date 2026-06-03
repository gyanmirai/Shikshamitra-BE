using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Policies;

public class PolicyHolidayImpactPreviewDto
{
	public Guid AcademicYearId { get; set; }
	public Guid? WorkCalendarPolicyId { get; set; }
	public Guid? PayrollPolicyId { get; set; }
	public Guid? AttendancePolicyId { get; set; }
	public List<PolicyHolidayImpactItemDto> Items { get; set; } = [];
}

public class PolicyHolidayImpactItemDto
{
	public DateTime Date { get; set; }
	public DateTime? FromDate { get; set; }
	public DateTime? ToDate { get; set; }
	public int DayCount { get; set; } = 1;
	public Guid CalendarHolidayId { get; set; }
	public string HolidayName { get; set; } = string.Empty;
	public Guid? WorkCalendarPolicyId { get; set; }
	public string? WorkCalendarPolicyName { get; set; }
	public string CalendarSource { get; set; } = string.Empty;
	public string AttendanceImpact { get; set; } = string.Empty;
	public bool IsAttendanceWorkingDay { get; set; }
	public bool IsPunchRequired { get; set; }
	public string HolidayWorkBehavior { get; set; } = string.Empty;
	public string PayrollImpact { get; set; } = string.Empty;
	public bool IsPaidInPayroll { get; set; }
	public decimal PayableUnit { get; set; }
	public decimal PayrollBasisDayDelta { get; set; }
	public string PayrollOverrideSource { get; set; } = string.Empty;
	public string AttendanceBehaviorText { get; set; } = string.Empty;
	public string PayrollBasisImpactText { get; set; } = string.Empty;
	public bool HasDifference { get; set; }
}

public class HrPolicyCloneRequestDto
{
	public string? Name { get; set; }
	public DateTime? EffectiveFrom { get; set; }
}

public class HrPolicyImpactPreviewRequestDto
{
	public string PolicyType { get; set; } = string.Empty;
	public Guid? PolicyId { get; set; }
	public Guid? AcademicYearId { get; set; }
	public string Action { get; set; } = "update";
	public Guid? ProposedPolicyId { get; set; }
	public CreateUpdateWorkCalendarPolicyDto? WorkCalendarPolicy { get; set; }
	public CreateUpdatePayrollPolicyDto? PayrollPolicy { get; set; }
	public CreateUpdateAttendancePolicyDto? AttendancePolicy { get; set; }
}

public class HrPolicyAssignmentImpactPreviewRequestDto
{
	public string Action { get; set; } = "create";
	public Guid? AssignmentId { get; set; }
	public CreateUpdateHrPolicyAssignmentDto Assignment { get; set; } = new();
}

public class HrPolicyImpactPreviewDto
{
	public string PolicyType { get; set; } = string.Empty;
	public Guid? PolicyId { get; set; }
	public Guid AcademicYearId { get; set; }
	public string Action { get; set; } = string.Empty;
	public int? CurrentVersion { get; set; }
	public int? ProposedVersion { get; set; }
	public int AffectedEmployeeCount { get; set; }
	public int AffectedAssignmentCount { get; set; }
	public List<HrPolicyAffectedEmployeeDto> AffectedEmployees { get; set; } = [];
	public List<HrPolicyAffectedPeriodDto> AffectedPayrollMonths { get; set; } = [];
	public List<HrPolicyAffectedPeriodDto> LockedPayrollMonths { get; set; } = [];
	public List<HrPolicyAffectedPeriodDto> AffectedAttendanceEvaluationMonths { get; set; } = [];
	public bool HasStalePayrollRisk { get; set; }
	public List<HrPolicyImpactWarningDto> Warnings { get; set; } = [];
	public List<HrPolicyEffectiveAssignmentConflictDto> EffectiveAssignmentConflicts { get; set; } = [];
	public bool RequiresConfirmation { get; set; }
	public string? ConfirmationToken { get; set; }
	public DateTime? ExpiresAt { get; set; }
	public List<HrPolicyImpactWarningDto> BlockingWarnings { get; set; } = [];
}

public class HrPolicyImpactWarningDto
{
	public string Severity { get; set; } = "info";
	public string Code { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
}

public class HrPolicyAffectedEmployeeDto
{
	public Guid EmployeeId { get; set; }
	public string EmployeeCode { get; set; } = string.Empty;
	public string EmployeeName { get; set; } = string.Empty;
	public string? Department { get; set; }
	public Guid? RoleId { get; set; }
	public Guid? ShiftId { get; set; }
	public string? EmployeeType { get; set; }
}

public class HrPolicyAffectedPeriodDto
{
	public int Year { get; set; }
	public int Month { get; set; }
	public string Label { get; set; } = string.Empty;
	public int Count { get; set; }
	public string Status { get; set; } = string.Empty;
}

public class HrPolicyAssignmentConflictDto
{
	public Guid AssignmentId { get; set; }
	public string TargetType { get; set; } = string.Empty;
	public string TargetKey { get; set; } = string.Empty;
	public string? TargetDisplayName { get; set; }
	public Guid PayrollPolicyId { get; set; }
	public string PayrollPolicyName { get; set; } = string.Empty;
	public Guid AttendancePolicyId { get; set; }
	public string AttendancePolicyName { get; set; } = string.Empty;
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public int Priority { get; set; }
	public List<string> SuggestedFixes { get; set; } = [];
}

public class HrPolicyEffectiveAssignmentConflictDto
{
	public Guid EmployeeId { get; set; }
	public string EmployeeCode { get; set; } = string.Empty;
	public string EmployeeName { get; set; } = string.Empty;
	public DateTime Date { get; set; }
	public HrPolicyEffectiveAssignmentCandidateDto? WinningAssignment { get; set; }
	public List<HrPolicyEffectiveAssignmentCandidateDto> CandidateAssignments { get; set; } = [];
}

public class HrPolicyEffectiveAssignmentCandidateDto
{
	public Guid? AssignmentId { get; set; }
	public string TargetType { get; set; } = string.Empty;
	public string TargetKey { get; set; } = string.Empty;
	public string? TargetDisplayName { get; set; }
	public Guid PayrollPolicyId { get; set; }
	public string PayrollPolicyName { get; set; } = string.Empty;
	public Guid AttendancePolicyId { get; set; }
	public string AttendancePolicyName { get; set; } = string.Empty;
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public int Priority { get; set; }
	public string Status { get; set; } = "skipped";
	public string SkipReason { get; set; } = "lower_priority";
}

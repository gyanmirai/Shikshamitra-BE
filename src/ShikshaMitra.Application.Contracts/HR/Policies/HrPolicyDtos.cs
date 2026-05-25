using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Policies;

public class PolicyListRequestDto : PagedAndSortedResultRequestDto
{
	public Guid AcademicYearId { get; set; }
	public string? Search { get; set; }
	public string? TargetType { get; set; }
}

public class WorkCalendarPolicyDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Status { get; set; } = "draft";
	public DateTime? EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public string WeeklyOffPattern { get; set; } = "saturday_sunday";
	public List<int> CustomWeeklyOffs { get; set; } = [];
	public string HolidaySyncMode { get; set; } = "linked_with_overrides";
	public List<WorkCalendarHolidayDto> Holidays { get; set; } = [];
	public WorkCalendarHolidaySummaryDto HolidaySummary { get; set; } = new();
	public int Version { get; set; }
	public DateTime CreationTime { get; set; }
	public Guid? CreatorId { get; set; }
	public DateTime? LastModificationTime { get; set; }
	public Guid? LastModifierId { get; set; }
}

public class CreateUpdateWorkCalendarPolicyDto
{
	public Guid AcademicYearId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Status { get; set; } = "draft";
	public DateTime? EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public string WeeklyOffPattern { get; set; } = "saturday_sunday";
	public List<int> CustomWeeklyOffs { get; set; } = [];
	public string HolidaySyncMode { get; set; } = "linked_with_overrides";
	public List<WorkCalendarHolidayDto> Holidays { get; set; } = [];
}

public class WorkCalendarHolidayDto
{
	public Guid? Id { get; set; }
	public Guid? WorkCalendarPolicyId { get; set; }
	public Guid AcademicYearId { get; set; }
	public DateTime Date { get; set; }
	public DateTime? FromDate { get; set; }
	public DateTime? ToDate { get; set; }
	public int DayCount { get; set; } = 1;
	public string HolidayName { get; set; } = string.Empty;
	public string Source { get; set; } = "central_calendar";
	public Guid? CalendarHolidayId { get; set; }
	public bool IsIncluded { get; set; } = true;
	public string? Notes { get; set; }
}

public class WorkCalendarHolidaySummaryDto
{
	public int TotalCount { get; set; }
	public int IncludedCount { get; set; }
	public int RemovedCount { get; set; }
	public int ManualCount { get; set; }
	public int CentralCount { get; set; }
}

public class PayrollPolicyDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public Guid WorkCalendarPolicyId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Status { get; set; } = "draft";
	public bool IsDefault { get; set; }
	public string DayCalculationType { get; set; } = "working-days";
	public int? ManualDays { get; set; }
	public List<PayrollPolicyManualMonthDto> ManualMonthDays { get; set; } = [];
	public string SaturdayWorkPolicy { get; set; } = "full-day";
	public List<PayrollPolicyHolidayOverrideDto> HolidayOverrides { get; set; } = [];
	public int Version { get; set; }
	public DateTime CreationTime { get; set; }
	public Guid? CreatorId { get; set; }
	public DateTime? LastModificationTime { get; set; }
	public Guid? LastModifierId { get; set; }
}

public class CreateUpdatePayrollPolicyDto
{
	public Guid AcademicYearId { get; set; }
	public Guid WorkCalendarPolicyId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Status { get; set; } = "draft";
	public bool IsDefault { get; set; }
	public string DayCalculationType { get; set; } = "working-days";
	public int? ManualDays { get; set; }
	public List<PayrollPolicyManualMonthDto> ManualMonthDays { get; set; } = [];
	public string SaturdayWorkPolicy { get; set; } = "full-day";
	public List<PayrollPolicyHolidayOverrideDto> HolidayOverrides { get; set; } = [];
}

public class PayrollPolicyManualMonthDto
{
	public int Year { get; set; }
	public int Month { get; set; }
	public int ManualDays { get; set; }
}

public class PayrollPolicyHolidayOverrideDto
{
	public DateTime Date { get; set; }
	public string HolidayName { get; set; } = string.Empty;
	public bool IncludedInPayroll { get; set; } = true;
	public Guid? CalendarHolidayId { get; set; }
}

public class AttendancePolicyDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public Guid WorkCalendarPolicyId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Status { get; set; } = "draft";
	public bool IsDefault { get; set; }
	public double FullDayThresholdPercentage { get; set; }
	public double HalfDayThresholdPercentage { get; set; }
	public double AbsentThresholdPercentage { get; set; }
	public string MissingPunchBehavior { get; set; } = "requires_review";
	public bool ManualCorrectionAllowed { get; set; }
	public int GraceTime { get; set; }
	public int MaxLateAllowedPerMonth { get; set; }
	public string PenaltyType { get; set; } = "none";
	public int PenaltyCountThreshold { get; set; }
	public bool EarlyExitEnabled { get; set; }
	public int EarlyExitGraceTime { get; set; }
	public bool OvertimeEnabled { get; set; }
	public string? OvertimeRate { get; set; }
	public int? MinOvertimeMinutes { get; set; }
	public string HolidayWorkBehavior { get; set; } = "comp_off";
	public bool SandwichRuleEnabled { get; set; }
	public bool PayrollPreviewEnabled { get; set; }
	public int Version { get; set; }
	public DateTime CreationTime { get; set; }
	public Guid? CreatorId { get; set; }
	public DateTime? LastModificationTime { get; set; }
	public Guid? LastModifierId { get; set; }
}

public class CreateUpdateAttendancePolicyDto
{
	public Guid AcademicYearId { get; set; }
	public Guid WorkCalendarPolicyId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Status { get; set; } = "draft";
	public bool IsDefault { get; set; }
	public double FullDayThresholdPercentage { get; set; } = 75;
	public double HalfDayThresholdPercentage { get; set; } = 50;
	public double AbsentThresholdPercentage { get; set; }
	public string MissingPunchBehavior { get; set; } = "requires_review";
	public bool ManualCorrectionAllowed { get; set; } = true;
	public int GraceTime { get; set; }
	public int MaxLateAllowedPerMonth { get; set; }
	public string PenaltyType { get; set; } = "none";
	public int PenaltyCountThreshold { get; set; }
	public bool EarlyExitEnabled { get; set; }
	public int EarlyExitGraceTime { get; set; }
	public bool OvertimeEnabled { get; set; }
	public string? OvertimeRate { get; set; }
	public int? MinOvertimeMinutes { get; set; }
	public string HolidayWorkBehavior { get; set; } = "comp_off";
	public bool SandwichRuleEnabled { get; set; }
	public bool PayrollPreviewEnabled { get; set; } = true;
}

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

public class HrPolicyAuditSummaryDto
{
	public string PolicyType { get; set; } = string.Empty;
	public Guid PolicyId { get; set; }
	public string Name { get; set; } = string.Empty;
	public int Version { get; set; }
	public DateTime CreationTime { get; set; }
	public Guid? CreatorId { get; set; }
	public string? CreatedByName { get; set; }
	public DateTime? LastModificationTime { get; set; }
	public Guid? LastModifierId { get; set; }
	public string? LastModifiedByName { get; set; }
	public string? LastDefaultSetSummary { get; set; }
	public string? LatestAssignmentReason { get; set; }
	public List<HrPolicyAuditEventDto> Events { get; set; } = [];
}

public class HrPolicyAuditEventDto
{
	public DateTime Timestamp { get; set; }
	public Guid? ActorId { get; set; }
	public string? ActorName { get; set; }
	public string Action { get; set; } = string.Empty;
	public string? SourceMethod { get; set; }
	public string? ChangedFieldsSummary { get; set; }
}

public class HrPolicyVersionDto : EntityDto<Guid>
{
	public string PolicyType { get; set; } = string.Empty;
	public Guid PolicyId { get; set; }
	public Guid AcademicYearId { get; set; }
	public int VersionNumber { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public DateTime? EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public string ChangeAction { get; set; } = string.Empty;
	public string? ChangeReason { get; set; }
	public DateTime CreationTime { get; set; }
	public Guid? CreatorId { get; set; }
	public string? CreatedByName { get; set; }
	public string Summary { get; set; } = string.Empty;
	public int ChangedFieldCount { get; set; }
	public string SnapshotJson { get; set; } = "{}";
}

public class HrPolicyVersionCompareDto
{
	public string PolicyType { get; set; } = string.Empty;
	public Guid PolicyId { get; set; }
	public HrPolicyVersionDto FromVersion { get; set; } = new();
	public HrPolicyVersionDto ToVersion { get; set; } = new();
	public List<HrPolicyVersionDifferenceDto> Differences { get; set; } = [];
}

public class HrPolicyVersionDifferenceDto
{
	public string Field { get; set; } = string.Empty;
	public string? FromValue { get; set; }
	public string? ToValue { get; set; }
	public string FieldKey { get; set; } = string.Empty;
	public string FieldLabel { get; set; } = string.Empty;
	public string Category { get; set; } = string.Empty;
	public string FromDisplayValue { get; set; } = string.Empty;
	public string ToDisplayValue { get; set; } = string.Empty;
	public string? FromRawValue { get; set; }
	public string? ToRawValue { get; set; }
}

public class HrPolicyRestoreDraftRequestDto
{
	public string? Name { get; set; }
	public DateTime? EffectiveFrom { get; set; }
	public string? ChangeReason { get; set; }
}

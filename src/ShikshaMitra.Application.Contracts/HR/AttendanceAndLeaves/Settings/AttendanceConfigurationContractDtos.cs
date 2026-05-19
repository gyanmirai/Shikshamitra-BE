using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class AttendanceConfigurationRequest
{
	public string PolicyName { get; set; } = "Default Attendance Policy";
	public string Status { get; set; } = "draft";
	public DateTime? EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }

	[Required]
	public AttendanceWorkdayRulesRequest WorkdayRules { get; set; } = new();

	[Required]
	public AttendancePunchRulesRequest PunchRules { get; set; } = new();

	[Required]
	public AttendanceDailyRulesRequest DailyRules { get; set; } = new();

	[Required]
	public AttendanceLateEarlyRulesRequest LateEarlyRules { get; set; } = new();

	[Required]
	public AttendanceOvertimeRulesRequest Overtime { get; set; } = new();

	public AttendanceHolidayRulesRequest HolidayRules { get; set; } = new();
	public AttendanceManualCorrectionRulesRequest ManualCorrectionRules { get; set; } = new();
	public bool PayrollPreviewEnabled { get; set; } = true;
	public string PayrollIntegrationMode { get; set; } = "advisory";
	public bool RequirePublishedAttendanceForPayrollLock { get; set; }
	public List<AttendancePolicyOverrideRequest> Overrides { get; set; } = [];
}

public class AttendanceWorkdayRulesRequest
{
	[RegularExpression("^(sunday|saturday_sunday|custom)$")]
	public string WeeklyOffPattern { get; set; } = "saturday_sunday";

	public List<int> CustomWeeklyOffs { get; set; } = [];
}

public class AttendancePunchRulesRequest
{
	[Range(0, 100)]
	public double? FullDayThresholdPercentage { get; set; } = 75;

	[Range(0, 100)]
	public double HalfDayThresholdPercentage { get; set; } = 50;

	[Range(0, 100)]
	public double AbsentThresholdPercentage { get; set; } = 0;

	[RegularExpression("^(mark_absent|mark_half_day|requires_review)$")]
	public string MissingPunchBehavior { get; set; } = "requires_review";
}

public class AttendanceDailyRulesRequest
{
	[Range(0, 100)]
	public double? HoursBasedPayPercentage { get; set; }
}

public class AttendanceLateEarlyRulesRequest
{
	[Range(0, int.MaxValue)]
	public int GraceTime { get; set; }

	[Range(0, int.MaxValue)]
	public int MaxLateAllowedPerMonth { get; set; }

	[Required]
	[RegularExpression("^(none|deduct_half_day|deduct_full_day)$")]
	public string PenaltyType { get; set; } = "none";

	[Range(0, int.MaxValue)]
	public int PenaltyCountThreshold { get; set; }

	public bool EarlyExitEnabled { get; set; }

	[Range(0, int.MaxValue)]
	public int EarlyExitGraceTime { get; set; }
}

public class AttendanceOvertimeRulesRequest
{
	public bool Enabled { get; set; }

	[RegularExpression("^(1.5x|2.0x|flat)$")]
	public string? Rate { get; set; }

	public int? MinOvertimeMinutes { get; set; }
}

public class AttendanceHolidayRulesRequest
{
	[RegularExpression("^(paid|comp_off|ot_payout)$")]
	public string WorkOnHolidayBehavior { get; set; } = "comp_off";

	public bool SandwichRule { get; set; }
}

public class AttendanceManualCorrectionRulesRequest
{
	public bool Allowed { get; set; } = true;
}

public class AttendancePolicyOverrideRequest
{
	[Required]
	[RegularExpression("^(department|role|employee_type|shift)$")]
	public string TargetType { get; set; } = string.Empty;

	[Required]
	public string TargetId { get; set; } = string.Empty;

	[Required]
	public string TargetName { get; set; } = string.Empty;

	public bool IsActive { get; set; } = true;
	public double? HoursBasedPayPercentage { get; set; }
	public int? GraceTime { get; set; }
	public int? MaxLateAllowedPerMonth { get; set; }
	public string? PenaltyType { get; set; }
	public bool? OvertimeEnabled { get; set; }
	public string? OvertimeRate { get; set; }
	public int? MinOvertimeMinutes { get; set; }
}

public class AttendanceConfigurationResponse
{
	public bool Success { get; set; }
	public string Message { get; set; } = string.Empty;
	public string PolicyName { get; set; } = "Default Attendance Policy";
	public string Status { get; set; } = "draft";
	public DateTime? EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public DateTime? LastUpdatedAt { get; set; }
	public AttendanceWorkdayRulesRequest? WorkdayRules { get; set; }
	public AttendancePunchRulesRequest? PunchRules { get; set; }
	public AttendanceDailyRulesRequest? DailyRules { get; set; }
	public AttendanceLateEarlyRulesRequest? LateEarlyRules { get; set; }
	public AttendanceOvertimeRulesRequest? Overtime { get; set; }
	public AttendanceHolidayRulesRequest? HolidayRules { get; set; }
	public AttendanceManualCorrectionRulesRequest? ManualCorrectionRules { get; set; }
	public bool PayrollPreviewEnabled { get; set; } = true;
	public string PayrollIntegrationMode { get; set; } = "advisory";
	public bool RequirePublishedAttendanceForPayrollLock { get; set; }
	public List<AttendancePolicyOverrideResponse> Overrides { get; set; } = [];
}

public class AttendancePolicyOverrideResponse : AttendancePolicyOverrideRequest
{
	public Guid Id { get; set; }
	public int Priority { get; set; }
}

public class AttendanceConfigurationPreviewRequest
{
	[Required]
	public DateTime FromDate { get; set; }

	[Required]
	public DateTime ToDate { get; set; }

	public string? DepartmentId { get; set; }
	public string? RoleId { get; set; }
	public string? EmployeeType { get; set; }
	public string? ShiftId { get; set; }

	[Required]
	public AttendanceConfigurationRequest Configuration { get; set; } = new();
}

public class AttendanceConfigurationPreviewResponse
{
	public int AffectedEmployeeCount { get; set; }
	public int EstimatedLateCount { get; set; }
	public int EstimatedAbsenceCount { get; set; }
	public int EstimatedOvertimeMinutes { get; set; }
	public double EstimatedPayableDayDelta { get; set; }
	public List<string> Warnings { get; set; } = [];
}

public class AttendancePolicyEvaluationRequest
{
	[Required]
	public Guid AcademicYearId { get; set; }

	[Range(1, 12)]
	public int Month { get; set; }

	[Range(1900, 3000)]
	public int Year { get; set; }

	public string? DepartmentId { get; set; }
	public string? RoleId { get; set; }
	public string? EmployeeType { get; set; }
	public string? ShiftId { get; set; }
}

public class AttendancePolicyEvaluationRunResponse
{
	public Guid Id { get; set; }
	public Guid AcademicYearId { get; set; }
	public Guid AttendanceConfigurationId { get; set; }
	public int Year { get; set; }
	public int Month { get; set; }
	public DateTime FromDate { get; set; }
	public DateTime ToDate { get; set; }
	public string Status { get; set; } = "draft";
	public int AffectedEmployeeCount { get; set; }
	public int EstimatedLateCount { get; set; }
	public int EstimatedAbsenceCount { get; set; }
	public int EstimatedOvertimeMinutes { get; set; }
	public double EstimatedPayableDayDelta { get; set; }
	public string ConfigHash { get; set; } = string.Empty;
	public DateTime? ApprovedAt { get; set; }
	public DateTime? PublishedAt { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime? LastUpdatedAt { get; set; }
	public long TotalResultCount { get; set; }
	public List<string> Warnings { get; set; } = [];
	public List<AttendancePolicyEvaluationResultResponse> Results { get; set; } = [];
}

public class AttendancePolicyEvaluationResultsRequest : PagedResultRequestDto
{
	public AttendancePolicyEvaluationResultsRequest()
	{
		MaxResultCount = 25;
	}

	public string? Search { get; set; }
	public DateTime? FromDate { get; set; }
	public DateTime? ToDate { get; set; }
	public string? EvaluatedStatus { get; set; }
	public bool? HasWarnings { get; set; }
	public bool? IsLate { get; set; }
	public bool? HasOvertime { get; set; }
	public string? DepartmentId { get; set; }
	public string? RoleId { get; set; }
	public string? EmployeeType { get; set; }
	public string? ShiftId { get; set; }
}

public class AttendancePolicyEvaluationResultResponse
{
	public Guid Id { get; set; }
	public Guid EmployeeId { get; set; }
	public string? EmployeeCode { get; set; }
	public string? EmployeeName { get; set; }
	public DateTime AttendanceDate { get; set; }
	public Guid? SourceAttendanceId { get; set; }
	public string BaselineStatus { get; set; } = string.Empty;
	public double BaselinePayableUnits { get; set; }
	public string EvaluatedStatus { get; set; } = string.Empty;
	public double EvaluatedPayableUnits { get; set; }
	public bool IsLate { get; set; }
	public bool IsEarlyExit { get; set; }
	public int OvertimeMinutes { get; set; }
	public List<string> Warnings { get; set; } = [];
	public bool IsManualOverride { get; set; }
}

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ShikshaMitra.HR.Dashboard;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HRDashboardDailyStatus { Present, Absent, OnLeave, HalfDay, NotMarked, NotScheduled, NeedsSetup }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HRDashboardSectionState { Available, Forbidden, Failed, Unavailable }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HRDashboardLeaveCoverage { FullDay, HalfDay }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HRDashboardReviewFlagCode
{
	FullDayLeaveConflict, IncompletePunch, CalendarNeedsConfiguration,
	AttendanceOnNonWorkingDay, RemainingHalfDayUnmarked, ConflictingAttendanceRecords
}

public sealed record HRDashboardReviewFlagDto(HRDashboardReviewFlagCode Code, string Message);

public class HRDashboardOverviewInput
{
	public string? Date { get; set; }
	public bool? IsTeaching { get; set; }
	public Guid? EducationMediumStageId { get; set; }
	public string? Department { get; set; }
	public Guid? ShiftId { get; set; }
}

public class HRDashboardStaffInput : HRDashboardOverviewInput
{
	public string? Search { get; set; }
	public string? Status { get; set; }
	public string Sorting { get; set; } = "name";
	public int SkipCount { get; set; }
	public int MaxResultCount { get; set; } = 25;
}

public class HRDashboardCapabilitiesDto
{
	public bool CanViewAttendance { get; set; }
	public bool CanViewStaff { get; set; }
	public bool CanViewEmployees { get; set; }
	public bool CanMarkAttendance { get; set; }
	public bool CanViewLeave { get; set; }
	public bool CanViewPayroll { get; set; }
	public bool CanOpenPayroll { get; set; }
	public bool CanViewOnboarding { get; set; }
	public bool CanOpenOnboarding { get; set; }
	public bool CanViewAppraisals { get; set; }
	public bool CanAddEmployees { get; set; }
}

public class HRDashboardOverviewDto
{
	public string Date { get; set; } = "";
	public string AsOfDate { get; set; } = "";
	public DateTimeOffset GeneratedAt { get; set; }
	public string ScopeLabel { get; set; } = "";
	public HRDashboardCapabilitiesDto Capabilities { get; set; } = new();
	public HRDashboardSectionState AttendanceState { get; set; } = HRDashboardSectionState.Forbidden;
	public HRDashboardSectionState WorkforceState { get; set; } = HRDashboardSectionState.Forbidden;
	public HRDashboardSectionState LeaveState { get; set; } = HRDashboardSectionState.Forbidden;
	public HRDashboardSectionState PayrollState { get; set; } = HRDashboardSectionState.Forbidden;
	public HRDashboardSectionState OnboardingState { get; set; } = HRDashboardSectionState.Forbidden;
	public HRDashboardSectionState AppraisalState { get; set; } = HRDashboardSectionState.Forbidden;
	public string? OnboardingUnavailableReason { get; set; }
	public HRDashboardOperationalWorkforceDto? Workforce { get; set; }
	public HRDashboardOperationalLeaveDto? Leave { get; set; }
	public HRDashboardOnboardingDto? Onboarding { get; set; }
	public HRDashboardAppraisalDto? Appraisal { get; set; }
	public HRDashboardDailyCountsDto? Summary { get; set; }
	public List<HRDashboardDailyCountsDto>? Trend { get; set; }
	public HRDashboardPayrollCycleDto? Payroll { get; set; }
}

public sealed record HRDashboardOperationalWorkforceDto(int ActiveStaff, int Teaching, int NonTeaching, int JoinedThisMonth);
public sealed record HRDashboardOperationalLeaveDto(int PendingRequests);
public sealed record HRDashboardOnboardingDto(int ReadyCount, int InProgressCount);
public sealed record HRDashboardAppraisalDto(int PendingCount, int OverdueCount, DateOnly? NextDeadline);

public class HRDashboardDailyCountsDto
{
	public string Date { get; set; } = "";
	public int StaffCount { get; set; }
	public int Present { get; set; }
	public int Absent { get; set; }
	public int OnLeave { get; set; }
	public int HalfDay { get; set; }
	public int NotMarked { get; set; }
	public int NotScheduled { get; set; }
	public int NeedsSetup { get; set; }
	public int NeedsReview { get; set; }
}

public class HRDashboardStaffDto
{
	public Guid EmployeeId { get; set; }
	public string Name { get; set; } = "";
	public string EmployeeCode { get; set; } = "";
	public bool IsTeaching { get; set; }
	public string? Department { get; set; }
	public List<HRDashboardOptionDto> Stages { get; set; } = [];
	public string? ShiftName { get; set; }
	public HRDashboardDailyStatus Status { get; set; } = HRDashboardDailyStatus.NotMarked;
	public HRDashboardLeaveCoverage? ApprovedLeaveCoverage { get; set; }
	public string? Source { get; set; }
	public DateTimeOffset? TimeIn { get; set; }
	public DateTimeOffset? TimeOut { get; set; }
	public List<HRDashboardReviewFlagDto> ReviewFlags { get; set; } = [];
}

public class HRDashboardStaffPageDto
{
	public string Date { get; set; } = "";
	public DateTimeOffset GeneratedAt { get; set; }
	public int TotalCount { get; set; }
	public List<HRDashboardStaffDto> Items { get; set; } = [];
}

public class HRDashboardFilterOptionsDto
{
	public List<string> Departments { get; set; } = [];
	public List<HRDashboardOptionDto> Stages { get; set; } = [];
	public List<HRDashboardOptionDto> Shifts { get; set; } = [];
}

public class HRDashboardOptionDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = "";
}

public class HRDashboardAttentionDto
{
	public string Kind { get; set; } = "";
	public string Title { get; set; } = "";
	public int Count { get; set; }
	public string Action { get; set; } = "";
}

public class HRDashboardPayrollCycleDto
{
	public string Month { get; set; } = "";
	public int SavedStaffCount { get; set; }
	public int ReviewedCount { get; set; }
	public int LockedCount { get; set; }
	public int PendingCount { get; set; }
	public DateTimeOffset? LastUpdatedAt { get; set; }
}

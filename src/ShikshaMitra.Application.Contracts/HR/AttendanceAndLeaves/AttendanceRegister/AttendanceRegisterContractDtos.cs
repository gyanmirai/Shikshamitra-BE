using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;

public enum AttendanceRegisterPayrollState
{
	All = 0,
	Editable = 1,
	Locked = 2
}

public enum AttendanceRegisterExceptionFilter
{
	All = 0,
	NeedsAttention = 1,
	Absent = 2,
	HalfDay = 3,
	MissingPunch = 4,
	Locked = 5
}

public enum AttendanceRegisterStatus
{
	NotMarked = 0,
	Present = 1,
	HalfDay = 2,
	Absent = 3
}

public class GetAttendanceRegisterInput : PagedAndSortedResultRequestDto
{
	public string StartDate { get; set; } = string.Empty;
	public string EndDate { get; set; } = string.Empty;
	public Guid? EducationMediumStageId { get; set; }
	public Guid? EmployeeId { get; set; }
	public EmployeeGroupType? EmployeeGroup { get; set; }
	public string? Department { get; set; }
	public Guid? RoleId { get; set; }
	public bool? IsTeaching { get; set; }
	public EmployeeAttendanceStatusType? Status { get; set; }
	public AttendanceRegisterPayrollState PayrollState { get; set; } = AttendanceRegisterPayrollState.All;
	public string? Search { get; set; }
}

public class GetGroupedAttendanceRegisterInput : GetAttendanceRegisterInput
{
	public AttendanceRegisterExceptionFilter ExceptionFilter { get; set; } = AttendanceRegisterExceptionFilter.All;
}

public class AttendanceRegisterResponse
{
	public int TotalCount { get; set; }
	public int CurrentPage { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public AttendanceRegisterSummaryDto Summary { get; set; } = new();
	public IReadOnlyList<AttendanceRegisterRowDto> Items { get; set; } = Array.Empty<AttendanceRegisterRowDto>();
}

public class AttendanceRegisterSummaryDto
{
	public int StaffCount { get; set; }
	public int Present { get; set; }
	public int HalfDay { get; set; }
	public int Absent { get; set; }
	public int NotMarked { get; set; }
	public int Editable { get; set; }
	public int Locked { get; set; }
	public int MissingPunch { get; set; }
	public int Holiday { get; set; }
}

public class GroupedAttendanceRegisterResponse
{
	public int TotalCount { get; set; }
	public int CurrentPage { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public AttendanceRegisterSummaryDto Summary { get; set; } = new();
	public IReadOnlyList<GroupedAttendanceRegisterEmployeeDto> Items { get; set; } = Array.Empty<GroupedAttendanceRegisterEmployeeDto>();
}

public class GroupedAttendanceRegisterEmployeeDto
{
	public Guid EmployeeId { get; set; }
	public string EmployeeName { get; set; } = string.Empty;
	public string EmployeeCode { get; set; } = string.Empty;
	public string? Department { get; set; }
	public string? EmployeeGroup { get; set; }
	public Guid? EducationMediumStageId { get; set; }
	public string? EducationMediumStageName { get; set; }
	public Guid? RoleId { get; set; }
	public int RecordedDays { get; set; }
	public int Present { get; set; }
	public int HalfDay { get; set; }
	public int Absent { get; set; }
	public int Editable { get; set; }
	public int Locked { get; set; }
	public int MissingPunch { get; set; }
	public int Holiday { get; set; }
	public bool HasExceptions { get; set; }
	public IReadOnlyList<AttendanceRegisterRowDto> Days { get; set; } = Array.Empty<AttendanceRegisterRowDto>();
}

public class AttendanceRegisterRowDto
{
	public Guid? AttendanceId { get; set; }
	public Guid EmployeeId { get; set; }
	public string EmployeeName { get; set; } = string.Empty;
	public string EmployeeCode { get; set; } = string.Empty;
	public string? Department { get; set; }
	public string? EmployeeGroup { get; set; }
	public Guid? EducationMediumStageId { get; set; }
	public string? EducationMediumStageName { get; set; }
	public Guid? RoleId { get; set; }
	public string AttendanceDate { get; set; } = string.Empty;
	public string PayrollMonth { get; set; } = string.Empty;
	public string? InTime { get; set; }
	public string? OutTime { get; set; }
	public string? HoursWorked { get; set; }
	public string? Remarks { get; set; }
	public AttendanceRegisterStatus Status { get; set; }
	public bool IsMarked { get; set; }
	public bool IsDayLocked { get; set; }
	public bool IsPayrollLocked { get; set; }
	public bool IsHoliday { get; set; }
	public string? HolidayName { get; set; }
	public string? HolidayReason { get; set; }
	public bool CanEdit { get; set; }
	public string? LockReason { get; set; }
}

public class AttendanceRegisterAdjustRequest
{
	public Guid AttendanceId { get; set; }
	public string StatusWire { get; set; } = "PRESENT";
	public string? InTime { get; set; }
	public string? OutTime { get; set; }
	public string? Remarks { get; set; }
	public string? Reason { get; set; }
}

public class AttendanceRegisterBulkAdjustRequest
{
	public List<Guid> AttendanceIds { get; set; } = [];
	public string StatusWire { get; set; } = "PRESENT";
	public string? InTime { get; set; }
	public string? OutTime { get; set; }
	public string? Remarks { get; set; }
	public string? Reason { get; set; }
}

public class AttendanceRegisterAdjustmentResult
{
	public int UpdatedCount { get; set; }
	public int SkippedCount { get; set; }
	public IReadOnlyList<AttendanceRegisterAdjustmentRowResult> Rows { get; set; } = Array.Empty<AttendanceRegisterAdjustmentRowResult>();
}

public class AttendanceRegisterAdjustmentRowResult
{
	public Guid AttendanceId { get; set; }
	public Guid? EmployeeId { get; set; }
	public string? AttendanceDate { get; set; }
	public bool Updated { get; set; }
	public string? Reason { get; set; }
}

public interface IAttendanceRegisterAppService
{
	Task<AttendanceRegisterResponse> GetAsync(GetAttendanceRegisterInput input);
	Task<GroupedAttendanceRegisterResponse> GetGroupedAsync(GetGroupedAttendanceRegisterInput input);
	Task<AttendanceRegisterAdjustmentResult> AdjustAsync(AttendanceRegisterAdjustRequest input);
	Task<AttendanceRegisterAdjustmentResult> BulkAdjustAsync(AttendanceRegisterBulkAdjustRequest input);
}

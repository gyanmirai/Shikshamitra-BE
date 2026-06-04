using System;
using System.Collections.Generic;
using ShikshaMitra.Common;
using ShikshaMitra.HR;
using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

internal sealed class EmployeeProjection
{
	public Guid EmployeeId { get; set; }
	public string EmployeeName { get; set; } = string.Empty;
	public string EmployeeCode { get; set; } = string.Empty;
	public string? MobileNo { get; set; }
	public string? Department { get; set; }
	public EmployeeGroupType? EmployeeGroup { get; set; }
	public Guid? EducationMediumStageId { get; set; }
	public List<Guid> AssignedStageIds { get; set; } = [];
	public Guid? RoleId { get; set; }
	public DateOnly? DateOfJoining { get; set; }
	public DateTime CreationTime { get; set; }
	public EmployeeStatusType? EmployeeStatusType { get; set; }
	public DateTimeOffset? StatusLastChangedAt { get; set; }
	public DateOnly? StatusEffectiveDate { get; set; }
}

internal sealed class RegisterProjection
{
	public Guid? AttendanceId { get; set; }
	public Guid EmployeeId { get; set; }
	public DateOnly AttendanceDate { get; set; }
	public AttendanceRegisterStatus Status { get; set; }
	public bool IsMarked { get; set; }
	public Guid? EducationMediumStageId { get; set; }
	public string EmployeeName { get; set; } = string.Empty;
	public string EmployeeCode { get; set; } = string.Empty;
	public string? MobileNo { get; set; }
	public string? Department { get; set; }
	public EmployeeGroupType? EmployeeGroup { get; set; }
	public Guid? RoleId { get; set; }
	public DateTimeOffset? TimeIn { get; set; }
	public DateTimeOffset? TimeOut { get; set; }
	public string? Remarks { get; set; }
	public AttendanceCaptureMode CaptureMode { get; set; } = AttendanceCaptureMode.Integrated;
	public AttendanceRecordSource Source { get; set; } = AttendanceRecordSource.Manual;
	public bool IsHoliday { get; set; }
	public string? HolidayName { get; set; }
}

internal sealed class GroupedRegisterProjection
{
	public Guid EmployeeId { get; set; }
	public string EmployeeName { get; set; } = string.Empty;
	public string EmployeeCode { get; set; } = string.Empty;
	public string? Department { get; set; }
	public EmployeeGroupType? EmployeeGroup { get; set; }
	public Guid? EducationMediumStageId { get; set; }
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
	public List<RegisterProjection> RawDays { get; set; } = [];
}

internal sealed record HolidayInfo(string Name);

internal sealed record PayrollLockInfo(bool IsLocked, string? Reason);

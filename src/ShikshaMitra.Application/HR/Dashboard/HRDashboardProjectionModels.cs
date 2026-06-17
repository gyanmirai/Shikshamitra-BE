using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using System;

namespace ShikshaMitra.HR.Dashboard;

internal sealed record ActiveEmployeeDashboardRow(
	Guid Id,
	string EmployeeName,
	string? EmployeeCode,
	EmployeeGroupType? EmployeeGroup,
	DateOnly? DateOfJoining,
	DateOnly? DateOfBirth);

internal sealed record AttendanceStatusCount(
	DateOnly AttendanceDate,
	EmployeeAttendanceStatusType StatusType,
	int Count);

internal sealed record LeaveTrendRow(
	Guid EmployeeId,
	DateOnly StartDate,
	DateOnly EndDate);

internal sealed record LeaveDashboardRow(
	Guid Id,
	int PublicId,
	Guid EmployeeId,
	LeavePaidType PaidType,
	DateTimeOffset StartDate,
	DateTimeOffset EndDate,
	bool HalfDay,
	EmployeeLeaveStatusType Status,
	DateTime CreationTime);

internal sealed record LeaveTypeCountRow(
	LeavePaidType PaidType,
	int Count);

internal sealed record InactiveReportingManagerIssue(
	int ActiveDirectReportCount,
	int ManagerCount);

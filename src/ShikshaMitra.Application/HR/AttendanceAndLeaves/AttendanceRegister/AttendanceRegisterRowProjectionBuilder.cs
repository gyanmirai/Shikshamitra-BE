using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;
using ShikshaMitra.HR.Employees;
using Volo.Abp.DependencyInjection;
using static ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegisterProjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class AttendanceRegisterRowProjectionBuilder : ITransientDependency
{
	internal List<RegisterProjection> Build(
		GetAttendanceRegisterInput input,
		DateOnly startDate,
		DateOnly endDate,
		List<EmployeeProjection> employees,
		Dictionary<(Guid EmployeeId, DateOnly Date), EmployeeAttendance> attendanceByEmployeeDate,
		Dictionary<(Guid EmployeeId, DateOnly Date), HolidayInfo> holidayLookup,
		Dictionary<(Guid EmployeeId, DateOnly Date), AttendanceCaptureMode> captureModeLookup)
	{
		var rows = new List<RegisterProjection>();
		foreach (var employee in employees)
		{
			AddEmployeeRows(
				rows,
				input,
				startDate,
				endDate,
				employee,
				attendanceByEmployeeDate,
				holidayLookup,
				captureModeLookup);
		}

		return ApplySearch(rows, input.Search);
	}

	private static void AddEmployeeRows(
		List<RegisterProjection> rows,
		GetAttendanceRegisterInput input,
		DateOnly startDate,
		DateOnly endDate,
		EmployeeProjection employee,
		Dictionary<(Guid EmployeeId, DateOnly Date), EmployeeAttendance> attendanceByEmployeeDate,
		Dictionary<(Guid EmployeeId, DateOnly Date), HolidayInfo> holidayLookup,
		Dictionary<(Guid EmployeeId, DateOnly Date), AttendanceCaptureMode> captureModeLookup)
	{
		for (var date = startDate; date <= endDate; date = date.AddDays(1))
		{
			if (!IsEmployeeEligibleOnDate(employee, date))
			{
				continue;
			}

			attendanceByEmployeeDate.TryGetValue((employee.EmployeeId, date), out var attendance);
			if (input.Status.HasValue && attendance?.StatusType != input.Status.Value)
			{
				continue;
			}

			holidayLookup.TryGetValue((employee.EmployeeId, date), out var holiday);
			var captureMode = captureModeLookup.GetValueOrDefault((employee.EmployeeId, date), AttendanceCaptureMode.Integrated);
			rows.Add(BuildRow(employee, date, attendance, holiday, captureMode));
		}
	}

	private static RegisterProjection BuildRow(
		EmployeeProjection employee,
		DateOnly date,
		EmployeeAttendance? attendance,
		HolidayInfo? holiday,
		AttendanceCaptureMode captureMode) =>
		new()
		{
			AttendanceId = attendance?.Id,
			EmployeeId = employee.EmployeeId,
			AttendanceDate = date,
			Status = attendance == null
				? AttendanceRegisterStatus.NotMarked
				: MapRegisterStatus(attendance.StatusType),
			IsMarked = attendance != null,
			EducationMediumStageId = ResolveRegisterDisplayStage(attendance, employee),
			EmployeeName = employee.EmployeeName,
			EmployeeCode = employee.EmployeeCode,
			MobileNo = employee.MobileNo,
			Department = employee.Department,
			EmployeeGroup = employee.EmployeeGroup,
			RoleId = employee.RoleId,
			TimeIn = attendance?.TimeIn,
			TimeOut = attendance?.TimeOut,
			Remarks = attendance?.Remarks,
			CaptureMode = captureMode,
			Source = attendance?.Source ?? AttendanceRecordSource.Manual,
			IsHoliday = holiday != null,
			HolidayName = holiday?.Name
		};

	private static List<RegisterProjection> ApplySearch(List<RegisterProjection> rows, string? search)
	{
		if (string.IsNullOrWhiteSpace(search))
		{
			return rows;
		}

		var trimmed = search.Trim();
		return rows.Where(x => MatchesSearch(x, trimmed)).ToList();
	}

	private static Guid? ResolveRegisterDisplayStage(EmployeeAttendance? attendance, EmployeeProjection employee)
	{
		if (attendance?.EducationMediumStageId.HasValue == true &&
		    employee.AssignedStageIds.Contains(attendance.EducationMediumStageId.Value))
		{
			return attendance.EducationMediumStageId;
		}

		return employee.EducationMediumStageId;
	}
}

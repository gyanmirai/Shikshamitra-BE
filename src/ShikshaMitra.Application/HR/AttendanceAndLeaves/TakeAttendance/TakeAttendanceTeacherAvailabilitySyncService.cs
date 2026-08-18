using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable.Substitutions;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;

[RemoteService(false)]
public class TakeAttendanceTeacherAvailabilitySyncService(
	IRepository<TeacherAvailability, Guid> availabilityRepository,
	IEmployeeAttendanceRepository attendanceRepository,
	IRepository<Employee, Guid> employeeRepository,
	ITeacherAvailabilityAppService teacherAvailabilityAppService)
	: ApplicationService, ITransientDependency
{
	private const string FullDayStartTime = "09:00";
	private const string FullDayEndTime = "14:15";
	private const string HalfDayStartTime = "09:00";
	private const string HalfDayEndTime = "12:00";

	public async Task SyncDateRangeAsync(DateTime fromDate, DateTime toDate)
	{
		var from = DateOnly.FromDateTime(fromDate.Date);
		var to = DateOnly.FromDateTime(toDate.Date);
		var attendanceQuery = await attendanceRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(
			attendanceQuery.Where(x => x.AttendanceDate >= from && x.AttendanceDate <= to));

		if (rows.Count == 0)
		{
			return;
		}

		var employeeIds = rows.Select(row => row.EmployeeId).Distinct().ToList();
		var employees = await employeeRepository.GetListAsync(x => employeeIds.Contains(x.Id));
		var employeeById = employees.ToDictionary(x => x.Id);

		foreach (var row in rows)
		{
			if (employeeById.TryGetValue(row.EmployeeId, out var employee))
			{
				await SyncAsync(new TakeAttendanceSavedEntry(employee, row));
			}
		}
	}

	public async Task SyncAsync(TakeAttendanceSavedEntry savedEntry)
	{
		var attendance = savedEntry.Attendance;
		var employee = savedEntry.Employee;
		var availabilityDate = DateTime.SpecifyKind(
			attendance.AttendanceDate.ToDateTime(TimeOnly.MinValue),
			DateTimeKind.Utc);

		var activeAttendanceRows = await availabilityRepository.GetListAsync(x =>
			!x.IsCancelled &&
			x.SourceType == TeacherAvailabilitySource.Attendance &&
			(
				x.SourceEntityId == attendance.Id ||
				(x.TeacherId == attendance.EmployeeId && x.AvailabilityDate == availabilityDate)
			));

		if (attendance.StatusType == EmployeeAttendanceStatusType.Present)
		{
			foreach (var row in activeAttendanceRows)
			{
				row.Cancel();
				await availabilityRepository.UpdateAsync(row, autoSave: false);
			}

			return;
		}

		var isFullDay = attendance.StatusType == EmployeeAttendanceStatusType.FullDayLeave;
		var startTime = isFullDay ? FullDayStartTime : HalfDayStartTime;
		var endTime = isFullDay ? FullDayEndTime : HalfDayEndTime;
		var reason = isFullDay
			? "Marked absent in HR attendance."
			: "Marked half-day unavailable in HR attendance.";

		var existingEquivalent = activeAttendanceRows.Any(x =>
			x.StartTime.ToString("HH:mm") == startTime &&
			x.EndTime.ToString("HH:mm") == endTime &&
			x.IsFullDay == isFullDay &&
			x.AvailabilityStatus == TeacherAvailabilityStatus.Unavailable);

		if (existingEquivalent)
		{
			return;
		}

		foreach (var row in activeAttendanceRows)
		{
			row.Cancel();
			await availabilityRepository.UpdateAsync(row, autoSave: false);
		}

		await teacherAvailabilityAppService.CreateAsync(new CreateTeacherAvailabilityDto
		{
			TeacherId = employee.Id,
			TeacherName = string.IsNullOrWhiteSpace(employee.EmployeeName)
				? employee.EmployeeCode
				: employee.EmployeeName,
			DepartmentName = employee.Department ?? "Academic",
			AvailabilityDate = availabilityDate,
			StartTime = startTime,
			EndTime = endTime,
			AvailabilityStatus = TeacherAvailabilityStatus.Unavailable,
			SourceType = TeacherAvailabilitySource.Attendance,
			SourceEntityId = attendance.Id,
			Reason = reason,
			IsFullDay = isFullDay,
            NotifyLeadership = true,
			Remarks = attendance.Remarks
		});
	}
}

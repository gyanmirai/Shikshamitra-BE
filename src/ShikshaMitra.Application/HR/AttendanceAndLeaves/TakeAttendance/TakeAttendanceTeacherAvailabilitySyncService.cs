using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable.Substitutions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;

[RemoteService(false)]
public class TakeAttendanceTeacherAvailabilitySyncService(
	IRepository<TeacherAvailability, Guid> availabilityRepository,
	IEmployeeAttendanceRepository attendanceRepository,
	IRepository<Employee, Guid> employeeRepository,
	AttendanceAvailabilityWindowResolver windowResolver,
	IRepository<AcademicYear, Guid> academicYearRepository)
	: ApplicationService, ITransientDependency
{

	[UnitOfWork]
	public virtual async Task SyncDateRangeAsync(DateTime fromDate, DateTime toDate)
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

	[UnitOfWork]
	public virtual async Task SyncAsync(TakeAttendanceSavedEntry savedEntry)
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
		var window = await windowResolver.ResolveAsync(
			employee,
			isFullDay,
			new TimeOnly(9, 0),
			isFullDay ? new TimeOnly(14, 15) : new TimeOnly(12, 0));
		var startTime = window.Start.ToString("HH:mm");
		var endTime = window.End.ToString("HH:mm");
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

		// This is a derived attendance fact, inside the already authorized/scoped
		// save workflow. It must not invoke the administrator-only manual API.
		// Other sources may overlap; only attendance-owned records are replaced.
		var years = await academicYearRepository.GetQueryableAsync();
		var academicYearId = await AsyncExecuter.FirstOrDefaultAsync(years
			.Where(year => year.StartDate.Date <= availabilityDate.Date && year.EndDate.Date >= availabilityDate.Date)
			.OrderByDescending(year => year.StartDate)
			.Select(year => (Guid?) year.Id));
		await availabilityRepository.InsertAsync(new TeacherAvailability(
			GuidGenerator.Create(), CurrentTenant.Id,
			string.IsNullOrWhiteSpace(employee.EmployeeName) ? employee.EmployeeCode : employee.EmployeeName,
			availabilityDate, window.Start, window.End,
			TeacherAvailabilityStatus.Unavailable, TeacherAvailabilitySource.Attendance,
			reason, isFullDay, true, academicYearId, employee.Id,
			employee.Department ?? "Academic", attendance.Id, remarks: attendance.Remarks), autoSave: true);
	}
}

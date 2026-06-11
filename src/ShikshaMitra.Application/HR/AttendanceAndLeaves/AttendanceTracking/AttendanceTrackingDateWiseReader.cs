using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceTracking;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[RemoteService(false)]
public class AttendanceTrackingDateWiseReader(
	IEmployeeAttendanceRepository attendanceRepository,
	IEmployeeLeaveRepository leaveRepository,
	HrEmployeeScopeQueryService employeeScopeQueryService)
	: ApplicationService, ITransientDependency
{
	public async Task<AttendanceDateWiseResponse> GetDateWiseAsync(
		GetAttendanceDateWiseInput input,
		UserScopeContext scope)
	{
		var targetDate = AttendanceTrackingFormatting.ParseDate(input.AttendanceDate);
		var targetDateOnly = DateOnly.FromDateTime(targetDate);

		var attendanceQuery = await attendanceRepository.GetQueryableAsync();
		var leaveQuery = await leaveRepository.GetQueryableAsync();
		var employeeQuery = await employeeScopeQueryService.BuildScopedEmployeeQueryAsync(
			scope,
			new HrEmployeeScopeOptions
			{
				EducationMediumStageId = input.EducationMediumStageId,
				ActiveOnOrAfter = targetDateOnly,
				JoinedOnOrBefore = targetDateOnly,
				TreatNullStatusAsActive = true
			});

		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var normalizedSearch = input.Search.Trim().ToLowerInvariant();
			employeeQuery = employeeQuery.Where(e =>
				((e.FirstName ?? string.Empty) + " " + (e.MiddleName ?? string.Empty) + " " + (e.LastName ?? string.Empty))
					.ToLower().Contains(normalizedSearch) ||
				(e.EmployeeCode ?? string.Empty).ToLower().Contains(normalizedSearch) ||
				(e.MobileNo ?? string.Empty).ToLower().Contains(normalizedSearch));
		}

		var employees = await AsyncExecuter.ToListAsync(employeeQuery);
		if (employees.Count == 0)
		{
			return new AttendanceDateWiseResponse
			{
				CurrentPage = input.Page,
				PageSize = input.PageSize,
				TotalPages = 0
			};
		}

		var employeeIds = employees.Select(e => e.Id).ToList();
		var attendanceRows = await AsyncExecuter.ToListAsync(
			attendanceQuery.Where(a =>
				employeeIds.Contains(a.EmployeeId) &&
				a.AttendanceDate == targetDateOnly &&
				a.EducationMediumStageId == input.EducationMediumStageId));
		var attendanceByEmployee = attendanceRows
			.GroupBy(a => a.EmployeeId)
			.ToDictionary(
				g => g.Key,
				g => g.OrderByDescending(a => a.LastModificationTime ?? a.CreationTime).First());
		var attendanceEmployeeIdsForDate = attendanceByEmployee.Keys.ToList();

		var targetDateStart = new DateTimeOffset(targetDate.Date, TimeSpan.Zero);
		var targetDateEnd = targetDateStart.AddDays(1).AddTicks(-1);

		var rejectedLeaveCandidates = await AsyncExecuter.ToListAsync(
			leaveQuery.Where(l =>
				l.Status == EmployeeLeaveStatusType.Rejected &&
				attendanceEmployeeIdsForDate.Contains(l.EmployeeId))
			.Select(l => new { l.EmployeeId, l.StartDate, l.EndDate }));
		var rejectedLeaveEmployeeIds = rejectedLeaveCandidates
			.Where(l => l.StartDate <= targetDateEnd && l.EndDate >= targetDateStart)
			.Select(l => l.EmployeeId)
			.Distinct()
			.ToList();

		var attendanceData = employees
			.Where(e => attendanceByEmployee.ContainsKey(e.Id))
			.Select(e => new
			{
				Employee = e,
				Attendance = attendanceByEmployee[e.Id],
				Status = AttendanceTrackingFormatting.MapToTrackingStatus(
					attendanceByEmployee[e.Id].StatusType,
					rejectedLeaveEmployeeIds.Contains(e.Id))
			})
			.Where(a => !input.Status.HasValue || a.Status == input.Status.Value)
			.OrderBy(a => a.Employee.FirstName)
			.ThenBy(a => a.Employee.LastName)
			.ToList();

		var totalCount = attendanceData.Count;
		var skipCount = (input.Page - 1) * input.PageSize;
		var items = attendanceData
			.Skip(skipCount)
			.Take(input.PageSize)
			.ToList();

		var summary = new AttendanceSummary
		{
			Present = attendanceByEmployee.Values.Count(a => a.StatusType == EmployeeAttendanceStatusType.Present),
			Absent = attendanceByEmployee.Values.Count(a => a.StatusType == EmployeeAttendanceStatusType.FullDayLeave),
			HalfDay = attendanceByEmployee.Values.Count(a => a.StatusType == EmployeeAttendanceStatusType.HalfDayLeave),
			RejectedLeave = rejectedLeaveEmployeeIds.Count
		};

		var responseItems = items.Select(a => new AttendanceDateWiseItem
		{
			EmployeeId = a.Employee.Id.ToString(),
			FullName = AttendanceTrackingFormatting.FormatEmployeeName(a.Employee.FirstName, a.Employee.MiddleName, a.Employee.LastName),
			EmployeeIdentifier = a.Employee.EmployeeCode ?? string.Empty,
			ContactNo = a.Employee.MobileNo ?? string.Empty,
			InTime = a.Attendance.TimeIn?.ToString("o"),
			OutTime = a.Attendance.TimeOut?.ToString("o"),
			HoursWorked = AttendanceTrackingFormatting.CalculateHoursWorked(a.Attendance.TimeIn, a.Attendance.TimeOut),
			Remarks = null,
			Status = a.Status
		}).ToList();

		return new AttendanceDateWiseResponse
		{
			TotalCount = totalCount,
			CurrentPage = input.Page,
			PageSize = input.PageSize,
			TotalPages = (int)Math.Ceiling((double)totalCount / input.PageSize),
			Summary = summary,
			Items = responseItems
		};
	}
}

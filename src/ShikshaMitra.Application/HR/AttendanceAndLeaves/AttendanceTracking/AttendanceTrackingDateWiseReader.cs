using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceTracking;
using ShikshaMitra.HR.Employees;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[RemoteService(false)]
public class AttendanceTrackingDateWiseReader(
	IEmployeeAttendanceRepository attendanceRepository,
	IEmployeeLeaveRepository leaveRepository,
	IRepository<Employee, Guid> employeeRepository)
	: ApplicationService, ITransientDependency
{
	public async Task<AttendanceDateWiseResponse> GetDateWiseAsync(GetAttendanceDateWiseInput input)
	{
		var targetDate = AttendanceTrackingFormatting.ParseDate(input.AttendanceDate);
		var targetDateOnly = DateOnly.FromDateTime(targetDate);

		var attendanceQuery = await attendanceRepository.GetQueryableAsync();
		var employeeQuery = await employeeRepository.GetQueryableAsync();
		var leaveQuery = await leaveRepository.GetQueryableAsync();

		attendanceQuery = attendanceQuery.Where(a => a.EducationMediumStageId == input.EducationMediumStageId);
		var attendanceEmployeeIdsForDate = await AsyncExecuter.ToListAsync(attendanceQuery
			.Where(a => a.AttendanceDate == targetDateOnly)
			.Select(a => a.EmployeeId)
			.Distinct());

		var attendanceData = from a in attendanceQuery
			where a.AttendanceDate == targetDateOnly
			join e in employeeQuery on a.EmployeeId equals e.Id
			select new
			{
				a.EmployeeId,
				a.TimeIn,
				a.TimeOut,
				a.StatusType,
				Employee = e
			};

		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var normalizedSearch = input.Search.Trim().ToLowerInvariant();
			attendanceData = attendanceData.Where(a =>
				((a.Employee.FirstName ?? string.Empty) + " " + (a.Employee.MiddleName ?? string.Empty) + " " + (a.Employee.LastName ?? string.Empty))
					.ToLower().Contains(normalizedSearch) ||
				(a.Employee.EmployeeCode ?? string.Empty).ToLower().Contains(normalizedSearch) ||
				(a.Employee.MobileNo ?? string.Empty).ToLower().Contains(normalizedSearch));
		}

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

		if (input.Status.HasValue)
		{
			attendanceData = input.Status.Value switch
			{
				AttendanceTrackingStatus.Present => attendanceData.Where(a =>
					a.StatusType == EmployeeAttendanceStatusType.Present),
				AttendanceTrackingStatus.Absent => attendanceData.Where(a =>
					a.StatusType == EmployeeAttendanceStatusType.FullDayLeave),
				AttendanceTrackingStatus.HalfDay => attendanceData.Where(a =>
					a.StatusType == EmployeeAttendanceStatusType.HalfDayLeave),
				AttendanceTrackingStatus.RejectedLeave => attendanceData.Where(a =>
					rejectedLeaveEmployeeIds.Contains(a.EmployeeId)),
				_ => attendanceData
			};
		}

		var totalCount = await AsyncExecuter.CountAsync(attendanceData);
		var skipCount = (input.Page - 1) * input.PageSize;
		var items = await AsyncExecuter.ToListAsync(
			attendanceData
				.OrderBy(a => a.Employee.FirstName)
				.ThenBy(a => a.Employee.LastName)
				.Skip(skipCount)
				.Take(input.PageSize));

		var allAttendanceForDate = await AsyncExecuter.ToListAsync(
			attendanceQuery.Where(a => a.AttendanceDate == targetDateOnly));

		var summary = new AttendanceSummary
		{
			Present = allAttendanceForDate.Count(a => a.StatusType == EmployeeAttendanceStatusType.Present),
			Absent = allAttendanceForDate.Count(a => a.StatusType == EmployeeAttendanceStatusType.FullDayLeave),
			HalfDay = allAttendanceForDate.Count(a => a.StatusType == EmployeeAttendanceStatusType.HalfDayLeave),
			RejectedLeave = rejectedLeaveEmployeeIds.Count
		};

		var responseItems = items.Select(a => new AttendanceDateWiseItem
		{
			EmployeeId = a.EmployeeId.ToString(),
			FullName = AttendanceTrackingFormatting.FormatEmployeeName(a.Employee.FirstName, a.Employee.MiddleName, a.Employee.LastName),
			EmployeeIdentifier = a.Employee.EmployeeCode ?? string.Empty,
			ContactNo = a.Employee.MobileNo ?? string.Empty,
			InTime = a.TimeIn?.ToString("o"),
			OutTime = a.TimeOut?.ToString("o"),
			HoursWorked = AttendanceTrackingFormatting.CalculateHoursWorked(a.TimeIn, a.TimeOut),
			Remarks = null,
			Status = AttendanceTrackingFormatting.MapToTrackingStatus(a.StatusType, rejectedLeaveEmployeeIds.Contains(a.EmployeeId))
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

using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceTracking;
using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

/// <summary>
/// Application service for attendance tracking (date-wise and employee-wise reports).
/// Uses DB-side paging/filtering for scalability.
/// </summary>
public class AttendanceTrackingAppService : ApplicationService, IAttendanceTrackingAppService
{
	private readonly IEmployeeAttendanceRepository _attendanceRepository;
	private readonly IEmployeeLeaveRepository _leaveRepository;
	private readonly IRepository<Employee, Guid> _employeeRepository;

	public AttendanceTrackingAppService(
		IEmployeeAttendanceRepository attendanceRepository,
		IEmployeeLeaveRepository leaveRepository,
		IRepository<Employee, Guid> employeeRepository)
	{
		_attendanceRepository = attendanceRepository;
		_leaveRepository = leaveRepository;
		_employeeRepository = employeeRepository;
	}

	/// <summary>
	/// Get attendance records for a specific date (paginated)
	/// </summary>
	public async Task<AttendanceDateWiseResponse> GetDateWiseAsync(GetAttendanceDateWiseInput input)
	{
		// Parse the date (supports DD-MM-YYYY and ISO formats)
		var targetDate = ParseDate(input.AttendanceDate);
		var targetDateOnly = DateOnly.FromDateTime(targetDate);

		// Get queryable for DB-side filtering
		var attendanceQuery = await _attendanceRepository.GetQueryableAsync();
		var employeeQuery = await _employeeRepository.GetQueryableAsync();
		var leaveQuery = await _leaveRepository.GetQueryableAsync();

		// Filter by tenant
		if (CurrentTenant.Id.HasValue)
		{
			attendanceQuery = attendanceQuery.Where(a => a.TenantId == CurrentTenant.Id);
			leaveQuery = leaveQuery.Where(l => l.TenantId == CurrentTenant.Id);
		}

		// Get all employees with attendance for this date
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

		// Get rejected leaves that overlap with the target date
		var targetDateStart = new DateTimeOffset(targetDate.Date, TimeSpan.Zero);
		var targetDateEnd = targetDateStart.AddDays(1).AddTicks(-1);

		var rejectedLeaveEmployeeIds = await AsyncExecuter.ToListAsync(
			leaveQuery
				.Where(l => l.Status == EmployeeLeaveStatusType.Rejected &&
							l.StartDate <= targetDateEnd &&
							l.EndDate >= targetDateStart)
				.Select(l => l.EmployeeId)
				.Distinct()
		);

		// Apply status filter if specified
		if (input.Status.HasValue)
		{
			attendanceData = input.Status.Value switch
			{
				AttendanceTrackingStatus.PRESENT => attendanceData.Where(a =>
					a.StatusType == EmployeeAttendanceStatusType.Present),
				AttendanceTrackingStatus.ABSENT => attendanceData.Where(a =>
					a.StatusType == EmployeeAttendanceStatusType.FullDayLeave),
				AttendanceTrackingStatus.HALF_DAY => attendanceData.Where(a =>
					a.StatusType == EmployeeAttendanceStatusType.HalfDayLeave),
				AttendanceTrackingStatus.REJECTED_LEAVE => attendanceData.Where(a =>
					rejectedLeaveEmployeeIds.Contains(a.EmployeeId)),
				_ => attendanceData
			};
		}

		// Get total count for pagination
		var totalCount = await AsyncExecuter.CountAsync(attendanceData);

		// Get paginated results
		var skipCount = (input.Page - 1) * input.PageSize;
		var items = await AsyncExecuter.ToListAsync(
			attendanceData
				.OrderBy(a => a.Employee.FirstName)
				.ThenBy(a => a.Employee.LastName)
				.Skip(skipCount)
				.Take(input.PageSize)
		);

		// Build summary - counts by status
		var allAttendanceForDate = await AsyncExecuter.ToListAsync(
			attendanceQuery.Where(a => a.AttendanceDate == targetDateOnly)
		);

		var summary = new AttendanceSummary
		{
			All = allAttendanceForDate.Count,
			Present = allAttendanceForDate.Count(a => a.StatusType == EmployeeAttendanceStatusType.Present),
			Absent = allAttendanceForDate.Count(a => a.StatusType == EmployeeAttendanceStatusType.FullDayLeave),
			HalfDay = allAttendanceForDate.Count(a => a.StatusType == EmployeeAttendanceStatusType.HalfDayLeave),
			RejectedLeave = rejectedLeaveEmployeeIds.Count
		};

		// Map to response items
		var responseItems = items.Select(a => new AttendanceDateWiseItem
		{
			EmployeeId = a.EmployeeId.ToString(),
			FullName = FormatEmployeeName(a.Employee.FirstName, a.Employee.MiddleName, a.Employee.LastName),
			EmployeeIdentifier = a.Employee.EmployeeCode ?? string.Empty,
			ContactNo = a.Employee.MobileNo ?? string.Empty,
			InTime = a.TimeIn?.ToString("HH:mm"),
			OutTime = a.TimeOut?.ToString("HH:mm"),
			HoursWorked = CalculateHoursWorked(a.TimeIn, a.TimeOut),
			Remarks = null,
			Status = MapToTrackingStatus(a.StatusType, rejectedLeaveEmployeeIds.Contains(a.EmployeeId))
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

	/// <summary>
	/// Get attendance records for a specific employee within a date range
	/// </summary>
	public async Task<AttendanceEmployeeWiseResponse> GetEmployeeWiseAsync(GetAttendanceEmployeeWiseInput input)
	{
		// Parse dates (DD-MM-YYYY format)
		var startDate = ParseDate(input.StartDate);
		var endDate = ParseDate(input.EndDate);

		if (endDate < startDate)
		{
			throw new UserFriendlyException("End date must be greater than or equal to start date");
		}

		var startDateOnly = DateOnly.FromDateTime(startDate);
		var endDateOnly = DateOnly.FromDateTime(endDate);

		// Get queryable for DB-side filtering
		var attendanceQuery = await _attendanceRepository.GetQueryableAsync();
		var leaveQuery = await _leaveRepository.GetQueryableAsync();

		// Filter by tenant
		if (CurrentTenant.Id.HasValue)
		{
			attendanceQuery = attendanceQuery.Where(a => a.TenantId == CurrentTenant.Id);
			leaveQuery = leaveQuery.Where(l => l.TenantId == CurrentTenant.Id);
		}

		// Get attendance records for the employee in the date range
		var attendanceRecords = await AsyncExecuter.ToListAsync(
			attendanceQuery
				.Where(a => a.EmployeeId == input.EmployeeId &&
							a.AttendanceDate >= startDateOnly &&
							a.AttendanceDate <= endDateOnly)
				.OrderBy(a => a.AttendanceDate)
		);

		// Get rejected leaves for this employee in the date range
		var startDateTime = new DateTimeOffset(startDate.Date, TimeSpan.Zero);
		var endDateTime = new DateTimeOffset(endDate.Date.AddDays(1).AddTicks(-1), TimeSpan.Zero);

		var rejectedLeaves = await AsyncExecuter.ToListAsync(
			leaveQuery
				.Where(l => l.EmployeeId == input.EmployeeId &&
							l.Status == EmployeeLeaveStatusType.Rejected &&
							l.StartDate <= endDateTime &&
							l.EndDate >= startDateTime)
		);

		// Build items for each day in the range
		var items = new List<AttendanceEmployeeWiseItem>();
		var summary = new AttendanceSummary();

		for (var date = startDate; date <= endDate; date = date.AddDays(1))
		{
			var dateOnly = DateOnly.FromDateTime(date);
			var attendance = attendanceRecords.FirstOrDefault(a => a.AttendanceDate == dateOnly);

			// Check if this date has a rejected leave
			var hasRejectedLeave = rejectedLeaves.Any(l =>
				l.StartDate.Date <= date.Date && l.EndDate.Date >= date.Date);

			AttendanceTrackingStatus status;
			if (hasRejectedLeave)
			{
				status = AttendanceTrackingStatus.REJECTED_LEAVE;
				summary.RejectedLeave++;
			}
			else if (attendance != null)
			{
				status = MapToTrackingStatus(attendance.StatusType, false);
				switch (status)
				{
					case AttendanceTrackingStatus.PRESENT:
						summary.Present++;
						break;
					case AttendanceTrackingStatus.ABSENT:
						summary.Absent++;
						break;
					case AttendanceTrackingStatus.HALF_DAY:
						summary.HalfDay++;
						break;
				}
			}
			else
			{
				// No attendance record = absent
				status = AttendanceTrackingStatus.ABSENT;
				summary.Absent++;
			}

			// Always count total days regardless of filter
			summary.All++;

			// Apply status filter if specified (skip adding to items but keep in summary counts)
			if (input.Status.HasValue && status != input.Status.Value)
			{
				continue;
			}

			items.Add(new AttendanceEmployeeWiseItem
			{
				Date = date.ToString("dd-MM-yyyy"),
				Day = date.DayOfWeek.ToString(),
				InTime = attendance?.TimeIn?.ToString("hh:mm tt"),
				OutTime = attendance?.TimeOut?.ToString("hh:mm tt"),
				HoursWorked = FormatHoursWorkedLong(attendance?.TimeIn, attendance?.TimeOut),
				Description = hasRejectedLeave ? "Leave Rejected" : null,
				Status = status
			});
		}

		return new AttendanceEmployeeWiseResponse
		{
			AttendanceSummary = summary,
			Items = items
		};
	}

	#region Private Helper Methods

	private static DateTime ParseDate(string dateString)
	{
		// Try DD-MM-YYYY format first
		if (DateTime.TryParseExact(dateString, "dd-MM-yyyy", CultureInfo.InvariantCulture,
			DateTimeStyles.None, out var result))
		{
			return result;
		}

		// Try ISO format
		if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
		{
			return result;
		}

		// Try DateTimeOffset parse
		if (DateTimeOffset.TryParse(dateString, out var dto))
		{
			return dto.DateTime;
		}

		throw new UserFriendlyException($"Invalid date format: {dateString}. Expected DD-MM-YYYY or ISO date-time format.");
	}

	private static string? CalculateHoursWorked(DateTimeOffset? timeIn, DateTimeOffset? timeOut)
	{
		if (!timeIn.HasValue || !timeOut.HasValue)
			return null;

		var duration = timeOut.Value - timeIn.Value;
		var hours = (int)duration.TotalHours;
		var minutes = duration.Minutes;
		return $"{hours:D2}:{minutes:D2}";
	}

	private static string? FormatHoursWorkedLong(DateTimeOffset? timeIn, DateTimeOffset? timeOut)
	{
		if (!timeIn.HasValue || !timeOut.HasValue)
			return null;

		var duration = timeOut.Value - timeIn.Value;
		var hours = (int)duration.TotalHours;
		var minutes = duration.Minutes;
		return $"{hours:D2}H {minutes:D2}M";
	}

	private static AttendanceTrackingStatus MapToTrackingStatus(EmployeeAttendanceStatusType statusType, bool hasRejectedLeave)
	{
		if (hasRejectedLeave)
			return AttendanceTrackingStatus.REJECTED_LEAVE;

		return statusType switch
		{
			EmployeeAttendanceStatusType.Present => AttendanceTrackingStatus.PRESENT,
			EmployeeAttendanceStatusType.FullDayLeave => AttendanceTrackingStatus.ABSENT,
			EmployeeAttendanceStatusType.HalfDayLeave => AttendanceTrackingStatus.HALF_DAY,
			_ => AttendanceTrackingStatus.ABSENT
		};
	}

	/// <summary>
	/// Formats employee name, handling null/empty middle names to avoid double spaces
	/// </summary>
	private static string FormatEmployeeName(string? firstName, string? middleName, string? lastName)
	{
		var parts = new[] { firstName, middleName, lastName }
			.Where(s => !string.IsNullOrWhiteSpace(s));
		return string.Join(" ", parts);
	}

	#endregion
}


using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceTracking;
using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[RemoteService(false)]
public class AttendanceTrackingEmployeeWiseReader(
	IEmployeeAttendanceRepository attendanceRepository,
	IEmployeeLeaveRepository leaveRepository)
	: ApplicationService, ITransientDependency
{
	public async Task<AttendanceEmployeeWiseResponse> GetEmployeeWiseAsync(
		GetAttendanceEmployeeWiseInput input,
		IReadOnlyCollection<Guid> educationMediumStageIds)
	{
		var startDate = AttendanceTrackingFormatting.ParseDate(input.StartDate);
		var endDate = AttendanceTrackingFormatting.ParseDate(input.EndDate);

		if (endDate < startDate)
		{
			throw new BusinessException(HRErrorCodes.AttendanceEndDateBeforeStartDate);
		}

		const int maxDateRangeDays = 90;
		if ((endDate - startDate).Days > maxDateRangeDays)
		{
			throw new BusinessException(HRErrorCodes.AttendanceDateRangeTooLarge);
		}

		var startDateOnly = DateOnly.FromDateTime(startDate);
		var endDateOnly = DateOnly.FromDateTime(endDate);
		var stageIds = educationMediumStageIds.Distinct().ToList();

		var attendanceQuery = await attendanceRepository.GetQueryableAsync();
		var leaveQuery = await leaveRepository.GetQueryableAsync();

		var attendanceRecords = await AsyncExecuter.ToListAsync(
			attendanceQuery
				.Where(a => a.EmployeeId == input.EmployeeId &&
				            a.EducationMediumStageId.HasValue &&
				            stageIds.Contains(a.EducationMediumStageId.Value) &&
				            a.AttendanceDate >= startDateOnly &&
				            a.AttendanceDate <= endDateOnly)
				.OrderBy(a => a.AttendanceDate));

		var startDateTime = new DateTimeOffset(startDate.Date, TimeSpan.Zero);
		var endDateTime = new DateTimeOffset(endDate.Date.AddDays(1).AddTicks(-1), TimeSpan.Zero);

		var rejectedLeaves = (await AsyncExecuter.ToListAsync(
			leaveQuery
				.Where(l =>
					l.EmployeeId == input.EmployeeId &&
					l.Status == EmployeeLeaveStatusType.Rejected)))
			.Where(l => l.StartDate <= endDateTime && l.EndDate >= startDateTime)
			.ToList();

		var items = new List<AttendanceEmployeeWiseItem>();
		var summary = new AttendanceSummary();

		for (var date = startDate; date <= endDate; date = date.AddDays(1))
		{
			var dateOnly = DateOnly.FromDateTime(date);
			var attendance = attendanceRecords.FirstOrDefault(a => a.AttendanceDate == dateOnly);
			var hasRejectedLeave = rejectedLeaves.Any(l =>
				l.StartDate.Date <= date.Date && l.EndDate.Date >= date.Date);

			var status = AttendanceTrackingFormatting.ResolveEmployeeWiseStatus(attendance, hasRejectedLeave, summary);
			var isoDate = AttendanceTrackingFormatting.BuildDateIsoString(date, attendance?.TimeIn?.Offset);
			if (input.Status.HasValue && status != input.Status.Value)
			{
				continue;
			}

			var item = new AttendanceEmployeeWiseItem
			{
				Date = isoDate,
				Day = date.DayOfWeek.ToString(),
				InTime = attendance?.TimeIn?.ToString("o"),
				OutTime = attendance?.TimeOut?.ToString("o"),
				HoursWorked = AttendanceTrackingFormatting.CalculateHoursWorked(attendance?.TimeIn, attendance?.TimeOut),
				Description = hasRejectedLeave ? "Leave Rejected" : null,
				Status = status
			};

			if (!AttendanceTrackingFormatting.MatchesEmployeeWiseSearch(item, input.Search))
			{
				continue;
			}

			items.Add(item);
		}

		var totalCount = items.Count;
		var skipCount = (input.Page - 1) * input.PageSize;
		var pagedItems = items.Skip(skipCount).Take(input.PageSize).ToList();

		return new AttendanceEmployeeWiseResponse
		{
			TotalCount = totalCount,
			CurrentPage = input.Page,
			PageSize = input.PageSize,
			TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / input.PageSize),
			AttendanceSummary = summary,
			Items = pagedItems
		};
	}
}

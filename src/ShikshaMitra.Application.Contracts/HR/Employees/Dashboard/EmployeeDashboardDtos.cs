using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees.Dashboard;

public interface IEmployeeDashboardAppService : IApplicationService
{
	Task<EmployeeDashboardDto> GetAsync(Guid employeeId, DateTime? date = null);
}

public class EmployeeDashboardDto
{
	public Guid EmployeeId { get; set; }
	public DateTime Date { get; set; }
	public Guid? AcademicYearId { get; set; }
	public string? AcademicYearName { get; set; }
	public string? DataNotice { get; set; }
	public string TodayStatus { get; set; } = EmployeeDashboardStatus.NotMarked;
	public decimal? AttendancePercentage { get; set; }
	public int RecordedAttendanceDays { get; set; }
	public decimal? LeaveBalanceDays { get; set; }
	public bool HasUnlimitedLeave { get; set; }
	public bool IsLeaveBalanceAvailable { get; set; }
	public int PendingLeaveRequests { get; set; }
	public EmployeeDashboardHolidayDto? UpcomingHoliday { get; set; }
	public List<EmployeeDashboardAttendanceDayDto> AttendanceDays { get; set; } = [];
	public List<EmployeeDashboardAttendanceMonthDto> AttendanceTrend { get; set; } = [];
	public List<EmployeeDashboardHolidayDto> Holidays { get; set; } = [];
}

public class EmployeeDashboardAttendanceDayDto
{
	public DateOnly Date { get; set; }
	public string Status { get; set; } = string.Empty;
	public DateTimeOffset? TimeIn { get; set; }
	public DateTimeOffset? TimeOut { get; set; }
	public string? Remarks { get; set; }
}

public class EmployeeDashboardAttendanceMonthDto
{
	public string Month { get; set; } = string.Empty;
	public int Present { get; set; }
	public int HalfDay { get; set; }
	public int Absent { get; set; }
	public int HalfDayLeave { get; set; }
	public int FullDayLeave { get; set; }
}

public class EmployeeDashboardHolidayDto
{
	public string Name { get; set; } = string.Empty;
	public DateOnly FromDate { get; set; }
	public DateOnly ToDate { get; set; }
}

public static class EmployeeDashboardStatus
{
	public const string Present = "Present";
	public const string HalfDay = "HalfDay";
	public const string Absent = "Absent";
	public const string HalfDayLeave = "HalfDayLeave";
	public const string FullDayLeave = "FullDayLeave";
	public const string Holiday = "Holiday";
	public const string WeeklyOff = "WeeklyOff";
	public const string NotMarked = "NotMarked";
	public const string NotAvailable = "NotAvailable";
}

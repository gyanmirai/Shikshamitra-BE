using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
public class HrSetupReviewConfigurationReader(
	IRepository<WorkCalendarPolicy, Guid> calendars,
	IRepository<WorkCalendarHoliday, Guid> holidays,
	IRepository<AttendancePolicy, Guid> attendance,
	IRepository<PayrollPolicy, Guid> payroll,
	ILeavePolicyRepository leave) : ApplicationService, ITransientDependency
{
	public static string Hash(object value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));

	public async Task<HrSetupReviewDto> ReadAsync(AcademicYear year, string section)
	{
		var result = new HrSetupReviewDto { AcademicYearId = year.Id, Section = section };
		object configuration;
		var calendarRows = await calendars.GetListAsync(x => x.TenantId == year.TenantId && x.AcademicYearId == year.Id);
		var holidayRows = await holidays.GetListAsync(x => x.TenantId == year.TenantId && x.AcademicYearId == year.Id);
		bool CalendarUsable(Guid id) => calendarRows.Any(x => x.Id == id && x.Status == HrPolicyStatus.Active && HrSetupReviewValidity.Calendar(x));
		if (section == HrSetupReviewSections.Calendar)
		{
			configuration = new { Calendars = calendarRows.OrderBy(x => x.Id).Select(HrPolicyMapping.ToDto),
				Holidays = holidayRows.OrderBy(x => x.Id).Select(HrPolicyMapping.ToDto) };
			var active = calendarRows.Where(x => x.Status == HrPolicyStatus.Active).ToList();
			result.ConfigurationState = active.Count == 0 ? "missing" : active.All(HrSetupReviewValidity.Calendar) ? "valid" : "invalid";
			result.Details = active.Select(x => $"{x.Name}: {HrPolicyMapping.MapWeeklyOffPattern(x.WeeklyOffPattern)}; {HrPolicyMapping.MapHolidaySyncMode(x.HolidaySyncMode)}").ToList();
		}
		else if (section == HrSetupReviewSections.Attendance)
		{
			var rows = await attendance.GetListAsync(x => x.TenantId == year.TenantId && x.AcademicYearId == year.Id);
			configuration = new { Policies = rows.OrderBy(x => x.Id).Select(HrPolicyMapping.ToDto),
				Calendars = calendarRows.OrderBy(x => x.Id).Select(HrPolicyMapping.ToDto),
				Holidays = holidayRows.OrderBy(x => x.Id).Select(HrPolicyMapping.ToDto) };
			var defaults = rows.Where(x => x.IsDefault && x.Status == HrPolicyStatus.Active).ToList();
			result.ConfigurationState = defaults.Count == 0 ? "missing" : defaults.Count != 1 || !CalendarUsable(defaults[0].WorkCalendarPolicyId) || !HrSetupReviewValidity.Attendance(defaults[0]) ? "invalid" : "valid";
			result.Details = defaults.Select(x => FormattableString.Invariant($"{x.Name}: {HrPolicyMapping.MapCaptureMode(x.CaptureMode)}; full day {x.FullDayThresholdPercentage}%, half day {x.HalfDayThresholdPercentage}%, absent {x.AbsentThresholdPercentage}%")).ToList();
		}
		else if (section == HrSetupReviewSections.Payroll)
		{
			var rows = await payroll.GetListAsync(x => x.TenantId == year.TenantId && x.AcademicYearId == year.Id);
			configuration = new { Policies = rows.OrderBy(x => x.Id).Select(HrPolicyMapping.ToDto),
				Calendars = calendarRows.OrderBy(x => x.Id).Select(HrPolicyMapping.ToDto),
				Holidays = holidayRows.OrderBy(x => x.Id).Select(HrPolicyMapping.ToDto) };
			var defaults = rows.Where(x => x.IsDefault && x.Status == HrPolicyStatus.Active).ToList();
			result.ConfigurationState = defaults.Count == 0 ? "missing" : defaults.Count != 1 || !CalendarUsable(defaults[0].WorkCalendarPolicyId) || !HrSetupReviewValidity.Payroll(defaults[0]) ? "invalid" : "valid";
			result.Details = defaults.Select(x => $"{x.Name}: {x.DayCalculationType}; Saturday {x.SaturdayWorkPolicy}").ToList();
		}
		else if (section == HrSetupReviewSections.Leave)
		{
			var rows = (await leave.GetListByAcademicYearIdAsync(year.Id, includeInactive: true)).Where(x => x.TenantId == year.TenantId).OrderBy(x => x.Id).ToList();
			configuration = rows.Select(x => new { x.Id, x.PolicyName, x.Status, x.IsActive, x.IsDefault, x.EffectiveFrom, x.EffectiveTo,
				x.Description, Rules = x.Rules.OrderBy(r => r.Id).Select(LeavePolicyProjection.ToRuleItem) });
			var defaults = rows.Where(x => x.IsDefault && x.IsActive && x.Status == HrPolicyStatus.Active).ToList();
			result.ConfigurationState = defaults.Count == 0 ? "missing" : defaults.Count != 1 || !HrSetupReviewValidity.Leave(defaults[0]) ? "invalid" : "valid";
			result.Details = defaults.SelectMany(x => x.Rules.Where(r => r.IsActive).OrderBy(r => r.PaidType).Select(r =>
				$"{x.PolicyName}: {r.PaidType} — {(r.IsUnlimited ? "unlimited" : $"{r.LeaveCount} days")}")).ToList();
			if (defaults.Any(x => x.Rules.Any(r => r.IsActive && !r.IsUnlimited && r.LeaveCount == 0)))
				result.Notices.Add("Zero leave allocation needs review. Choose this school's allocation deliberately; recording a review does not add leave days.");
		}
		else throw new BusinessException(HrSetupReviewSections.Invalid);
		if (result.ConfigurationState != "valid") result.Notices.Add("Check missing, duplicate or inactive default policies and calendar references in settings.");
		result.Fingerprint = Hash(new { Schema = 1, year.TenantId, year.Id, year.StartDate, year.EndDate, Section = section, Configuration = configuration });
		return result;
	}
}

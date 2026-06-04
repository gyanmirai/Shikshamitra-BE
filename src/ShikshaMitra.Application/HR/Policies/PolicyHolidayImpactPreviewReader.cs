using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Calendar.Holidays;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
public class PolicyHolidayImpactPreviewReader(
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
	IRepository<WorkCalendarPolicy, Guid> workCalendarPolicyRepository,
	IRepository<WorkCalendarHoliday, Guid> workCalendarHolidayRepository,
	IRepository<Holiday, Guid> centralHolidayRepository)
	: ApplicationService, ITransientDependency
{
	public async Task<PolicyHolidayImpactPreviewDto> GetPreviewAsync(
		Guid academicYearId,
		Guid? workCalendarPolicyId,
		Guid? payrollPolicyId,
		Guid? attendancePolicyId,
		DateTime? fromDate,
		DateTime? toDate,
		string? month)
	{
		var attendancePolicy = attendancePolicyId.HasValue
			? await attendancePolicyRepository.GetAsync(attendancePolicyId.Value)
			: await GetDefaultAttendancePolicyAsync(academicYearId);
		var payrollPolicy = payrollPolicyId.HasValue
			? await payrollPolicyRepository.GetAsync(payrollPolicyId.Value)
			: await GetDefaultPayrollPolicyAsync(academicYearId);
		var workCalendarPolicy = workCalendarPolicyId.HasValue
			? await workCalendarPolicyRepository.GetAsync(workCalendarPolicyId.Value)
			: await workCalendarPolicyRepository.GetAsync(payrollPolicy.WorkCalendarPolicyId);

		var (rangeStart, rangeEnd) = ResolveHolidayRange(fromDate, toDate, month);
		var finalizedHolidays = await GetFinalizedHolidaysAsync(academicYearId, workCalendarPolicy);
		var holidays = finalizedHolidays
			.Where(x => x.IsIncluded)
			.Where(x =>
				(!rangeStart.HasValue || x.ToDate.Date >= rangeStart.Value) &&
				(!rangeEnd.HasValue || x.FromDate.Date <= rangeEnd.Value))
			.OrderBy(x => x.FromDate)
			.ToList();

		var payrollOverrides = payrollPolicy.GetHolidayOverrides()
			.GroupBy(x => (x.Date.Date, x.CalendarHolidayId))
			.ToDictionary(x => x.Key, x => x.First());

		var attendanceImpact = MapAttendanceImpact(attendancePolicy.HolidayWorkBehavior);
		var holidayWorkBehavior = MapHolidayWorkBehavior(attendancePolicy.HolidayWorkBehavior);
		var isPunchRequired = attendancePolicy.HolidayWorkBehavior == AttendanceAndLeaves.Settings.AttendanceHolidayWorkBehavior.OvertimePayout;
		var isAttendanceWorkingDay = isPunchRequired;
		var items = holidays.Select(holiday =>
		{
			var date = holiday.FromDate.Date;
			payrollOverrides.TryGetValue((date, holiday.CalendarHolidayId), out var payrollOverride);
			var isPaid = payrollOverride?.IncludedInPayroll ?? true;
			var payrollImpact = isPaid ? "paid" : "unpaid";
			var payrollOverrideSource = payrollOverride is null ? "policy_default" : "policy_override";
			return new PolicyHolidayImpactItemDto
			{
				Date = date,
				FromDate = holiday.FromDate,
				ToDate = holiday.ToDate,
				DayCount = (holiday.ToDate.Date - holiday.FromDate.Date).Days + 1,
				CalendarHolidayId = holiday.CalendarHolidayId ?? holiday.Id,
				HolidayName = holiday.HolidayName,
				WorkCalendarPolicyId = workCalendarPolicy.Id,
				WorkCalendarPolicyName = workCalendarPolicy.Name,
				CalendarSource = HrPolicyMapping.MapWorkCalendarHolidaySource(holiday.Source),
				AttendanceImpact = attendanceImpact,
				IsAttendanceWorkingDay = isAttendanceWorkingDay,
				IsPunchRequired = isPunchRequired,
				HolidayWorkBehavior = holidayWorkBehavior,
				PayrollImpact = payrollImpact,
				IsPaidInPayroll = isPaid,
				PayableUnit = isPaid ? 1m : 0m,
				PayrollBasisDayDelta = isPaid ? 0m : -1m,
				PayrollOverrideSource = payrollOverrideSource,
				AttendanceBehaviorText = BuildAttendanceBehaviorText(attendanceImpact, isPunchRequired, holidayWorkBehavior),
				PayrollBasisImpactText = isPaid ? "Included as one payable payroll unit." : "Excluded from payroll basis for this policy.",
				HasDifference = isAttendanceWorkingDay || attendanceImpact != payrollImpact
			};
		}).ToList();

		return new PolicyHolidayImpactPreviewDto
		{
			AcademicYearId = academicYearId,
			WorkCalendarPolicyId = workCalendarPolicy.Id,
			PayrollPolicyId = payrollPolicy.Id,
			AttendancePolicyId = attendancePolicy.Id,
			Items = items
		};
	}

	private async Task<List<WorkCalendarHoliday>> GetFinalizedHolidaysAsync(Guid academicYearId, WorkCalendarPolicy workCalendarPolicy)
	{
		var workCalendarHolidayQuery = await workCalendarHolidayRepository.GetQueryableAsync();
		var finalizedHolidays = await AsyncExecuter.ToListAsync(workCalendarHolidayQuery
			.Where(x =>
				x.AcademicYearId == academicYearId &&
				x.WorkCalendarPolicyId == workCalendarPolicy.Id));

		if (workCalendarPolicy.HolidaySyncMode != WorkCalendarHolidaySyncMode.LinkedWithOverrides)
		{
			return finalizedHolidays;
		}

		var existingCalendarHolidayIds = finalizedHolidays
			.Where(x => x.CalendarHolidayId.HasValue)
			.Select(x => x.CalendarHolidayId!.Value)
			.ToHashSet();
		var centralHolidayQuery = await centralHolidayRepository.GetQueryableAsync();
		var centralHolidays = await AsyncExecuter.ToListAsync(centralHolidayQuery
			.Where(x => x.AcademicYearId == academicYearId && !existingCalendarHolidayIds.Contains(x.Id)));
		finalizedHolidays.AddRange(centralHolidays.Select(x => new WorkCalendarHoliday(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			workCalendarPolicy.Id,
			x.StartDate.Date,
			x.EndDate.Date,
			x.HolidayName,
			WorkCalendarHolidaySource.CentralCalendar,
			x.Id,
			isIncluded: true,
			x.Description)));

		return finalizedHolidays;
	}

	private static (DateTime? Start, DateTime? End) ResolveHolidayRange(DateTime? fromDate, DateTime? toDate, string? month)
	{
		if (!string.IsNullOrWhiteSpace(month) &&
			DateTime.TryParse($"{month.Trim()}-01", out var monthDate))
		{
			var start = new DateTime(monthDate.Year, monthDate.Month, 1);
			return (start, start.AddMonths(1).AddDays(-1));
		}

		return (fromDate?.Date, toDate?.Date);
	}

	private static string MapAttendanceImpact(AttendanceAndLeaves.Settings.AttendanceHolidayWorkBehavior value) => value switch
	{
		AttendanceAndLeaves.Settings.AttendanceHolidayWorkBehavior.Paid => "non_working_paid",
		AttendanceAndLeaves.Settings.AttendanceHolidayWorkBehavior.OvertimePayout => "working_required_punch",
		_ => "non_working_comp_off"
	};

	private static string MapHolidayWorkBehavior(AttendanceAndLeaves.Settings.AttendanceHolidayWorkBehavior value) => value switch
	{
		AttendanceAndLeaves.Settings.AttendanceHolidayWorkBehavior.Paid => "paid",
		AttendanceAndLeaves.Settings.AttendanceHolidayWorkBehavior.OvertimePayout => "overtime_payout",
		_ => "comp_off"
	};

	private static string BuildAttendanceBehaviorText(string attendanceImpact, bool isPunchRequired, string holidayWorkBehavior) =>
		$"{attendanceImpact}; punch {(isPunchRequired ? "required" : "not required")}; behavior {holidayWorkBehavior}";

	private async Task<AttendancePolicy> GetDefaultAttendancePolicyAsync(Guid academicYearId)
	{
		var query = await attendancePolicyRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(x =>
			x.AcademicYearId == academicYearId &&
			x.IsDefault &&
			x.Status == HrPolicyStatus.Active))
			?? throw new BusinessException(HRErrorCodes.DefaultAttendancePolicyRequired);
	}

	private async Task<PayrollPolicy> GetDefaultPayrollPolicyAsync(Guid academicYearId)
	{
		var query = await payrollPolicyRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(x =>
			x.AcademicYearId == academicYearId &&
			x.IsDefault &&
			x.Status == HrPolicyStatus.Active))
			?? throw new BusinessException(HRErrorCodes.DefaultPayrollPolicyRequired);
	}
}

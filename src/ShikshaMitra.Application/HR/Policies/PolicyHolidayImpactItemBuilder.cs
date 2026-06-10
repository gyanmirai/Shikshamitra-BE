using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Policies;

public class PolicyHolidayImpactItemBuilder : ITransientDependency
{
	public List<PolicyHolidayImpactItemDto> Build(
		List<WorkCalendarHoliday> holidays,
		WorkCalendarPolicy workCalendarPolicy,
		PayrollPolicy payrollPolicy,
		AttendancePolicy attendancePolicy)
	{
		var payrollOverrides = payrollPolicy.GetHolidayOverrides()
			.GroupBy(x => (x.Date.Date, x.CalendarHolidayId))
			.ToDictionary(x => x.Key, x => x.First());
		var attendanceImpact = MapAttendanceImpact(attendancePolicy.HolidayWorkBehavior);
		var holidayWorkBehavior = MapHolidayWorkBehavior(attendancePolicy.HolidayWorkBehavior);
		var isPunchRequired = attendancePolicy.HolidayWorkBehavior is
			AttendanceHolidayWorkBehavior.OvertimePayout or AttendanceHolidayWorkBehavior.Both;
		var isAttendanceWorkingDay = isPunchRequired;

		return holidays.Select(holiday =>
		{
			var date = holiday.FromDate.Date;
			payrollOverrides.TryGetValue((date, holiday.CalendarHolidayId), out var payrollOverride);
			var isPaid = payrollOverride?.IncludedInPayroll ?? true;
			var payrollImpact = isPaid ? "paid" : "unpaid";
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
				PayrollOverrideSource = payrollOverride is null ? "policy_default" : "policy_override",
				AttendanceBehaviorText = BuildAttendanceBehaviorText(attendanceImpact, isPunchRequired, holidayWorkBehavior),
				PayrollBasisImpactText = isPaid ? "Included as one payable payroll unit." : "Excluded from payroll basis for this policy.",
				HasDifference = isAttendanceWorkingDay || attendanceImpact != payrollImpact
			};
		}).ToList();
	}

	private static string MapAttendanceImpact(AttendanceHolidayWorkBehavior value) => value switch
	{
		AttendanceHolidayWorkBehavior.Paid => "non_working_paid",
		AttendanceHolidayWorkBehavior.OvertimePayout => "working_required_punch",
		AttendanceHolidayWorkBehavior.NoBenefit => "non_working_no_benefit",
		AttendanceHolidayWorkBehavior.Both => "working_required_punch_with_comp_off",
		_ => "non_working_comp_off"
	};

	private static string MapHolidayWorkBehavior(AttendanceHolidayWorkBehavior value) => value switch
	{
		AttendanceHolidayWorkBehavior.NoBenefit => "no_benefit",
		AttendanceHolidayWorkBehavior.Paid => "paid",
		AttendanceHolidayWorkBehavior.Both => "both",
		AttendanceHolidayWorkBehavior.OvertimePayout => "overtime_payout",
		_ => "comp_off"
	};

	private static string BuildAttendanceBehaviorText(string attendanceImpact, bool isPunchRequired, string holidayWorkBehavior) =>
		$"{attendanceImpact}; punch {(isPunchRequired ? "required" : "not required")}; behavior {holidayWorkBehavior}";
}

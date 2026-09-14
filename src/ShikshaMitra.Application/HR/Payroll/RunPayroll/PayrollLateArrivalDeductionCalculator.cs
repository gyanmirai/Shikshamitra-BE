using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

internal static class PayrollLateArrivalDeductionCalculator
{
	public const string AdjustmentName = "Late arrival deduction";
	public const string AdjustmentReason = "Attendance policy late-arrival salary deduction";

	public static PayrollLateArrivalDeductionCalculation? Calculate(
		string internalMonthYear,
		int year,
		int month,
		Employee employee,
		IReadOnlyCollection<EmployeeAttendance> attendanceRecords,
		ShiftManagement? shift,
		AttendancePolicy policy)
	{
		if (policy.PenaltyType != AttendancePenaltyType.SalaryDeduction ||
		    !policy.PenaltyDeductionAmount.HasValue ||
		    policy.PenaltyDeductionAmount.Value <= 0m ||
		    shift == null)
		{
			return null;
		}

		var effectiveTrigger = Math.Max(
			policy.MaxLateAllowedPerMonth + 1,
			policy.PenaltyCountThreshold > 0 ? policy.PenaltyCountThreshold : 1);
		var lateDates = attendanceRecords
			.Where(x =>
				x.AttendanceDate.Year == year &&
				x.AttendanceDate.Month == month &&
				x.TimeIn.HasValue &&
				IsLateArrival(x.TimeIn.Value, shift, policy.GraceTime))
			.OrderBy(x => x.AttendanceDate)
			.Select(x => x.AttendanceDate)
			.ToList();
		var chargedCount = Math.Max(0, lateDates.Count - effectiveTrigger + 1);
		if (chargedCount == 0)
		{
			return null;
		}

		var amountPerLate = Math.Round(
			policy.PenaltyDeductionAmount.Value,
			2,
			MidpointRounding.AwayFromZero);
		var amount = Math.Round(chargedCount * amountPerLate, 2, MidpointRounding.AwayFromZero);
		if (amount <= 0m)
		{
			return null;
		}

		var chargedDates = lateDates
			.Skip(Math.Max(0, effectiveTrigger - 1))
			.Select(x => x.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
		var description = string.Format(
			CultureInfo.InvariantCulture,
			"{0} late arrival(s) charged at {1:0.##} each. Dates: {2}.",
			chargedCount,
			amountPerLate,
			string.Join(", ", chargedDates));

		return new PayrollLateArrivalDeductionCalculation(
			employee.Id,
			amount,
			description,
			CreateSourceId(internalMonthYear, employee.Id));
	}

	internal static bool IsLateArrival(DateTimeOffset timeIn, ShiftManagement shift, int graceTime) =>
		// Attendance's HH:mm contract stores clock values at offset zero. Npgsql's legacy reader
		// can return the same instant in the server's local offset; that must not add a late penalty.
		(timeIn.UtcDateTime.TimeOfDay - shift.StartTime.ToTimeSpan()).TotalMinutes > graceTime;

	private static Guid CreateSourceId(string internalMonthYear, Guid employeeId)
	{
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
			$"{PayrollAdjustmentSourceTypes.LateArrivalSalaryDeduction}|{internalMonthYear}|{employeeId:D}"));
		return new Guid(hash.Take(16).ToArray());
	}
}

internal sealed record PayrollLateArrivalDeductionCalculation(
	Guid EmployeeId,
	decimal Amount,
	string Description,
	Guid SourceId);

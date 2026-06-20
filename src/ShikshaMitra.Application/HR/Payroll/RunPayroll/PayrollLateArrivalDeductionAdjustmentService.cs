using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollLateArrivalDeductionAdjustmentService(
	IRepository<PayrollAdjustment, Guid> payrollAdjustmentRepository,
	IRepository<PayrollRunItemSnapshot, Guid> payrollRunItemSnapshotRepository,
	IGuidGenerator guidGenerator,
	ICurrentTenant currentTenant,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	private const string AdjustmentName = "Late arrival deduction";
	private const string AdjustmentReason = "Attendance policy late-arrival salary deduction";

	public async Task SyncAsync(
		string internalMonthYear,
		int year,
		int month,
		IReadOnlyCollection<Employee> employees,
		IReadOnlyDictionary<Guid, List<EmployeeAttendance>> attendanceByEmployee,
		IReadOnlyDictionary<Guid, HrResolvedPolicies> policiesByEmployee)
	{
		var employeeIds = employees.Select(x => x.Id).Distinct().ToList();
		if (employeeIds.Count == 0)
		{
			return;
		}

		var lockedEmployeeIds = await GetLockedEmployeeIdsAsync(internalMonthYear, employeeIds);
		var existingAdjustments = await GetExistingAdjustmentsAsync(internalMonthYear, employeeIds);
		var desiredByEmployee = new Dictionary<Guid, DesiredLateArrivalDeduction>();

		foreach (var employee in employees)
		{
			if (lockedEmployeeIds.Contains(employee.Id) ||
			    !policiesByEmployee.TryGetValue(employee.Id, out var policies))
			{
				continue;
			}

			var desired = BuildDesiredAdjustment(
				internalMonthYear,
				year,
				month,
				employee,
				attendanceByEmployee.TryGetValue(employee.Id, out var attendanceRecords)
					? attendanceRecords
					: [],
				policies.AttendancePolicy);

			if (desired != null)
			{
				desiredByEmployee[employee.Id] = desired;
			}
		}

		var updatedSourceIds = new HashSet<Guid>();
		foreach (var existing in existingAdjustments)
		{
			if (lockedEmployeeIds.Contains(existing.EmployeeId))
			{
				continue;
			}

			if (desiredByEmployee.TryGetValue(existing.EmployeeId, out var desired))
			{
				existing.UpdatePostingScope(internalMonthYear, existing.EmployeeId);
				existing.SetSource(
					PayrollAdjustmentSourceTypes.LateArrivalSalaryDeduction,
					desired.SourceId,
					isSystemGenerated: true);
				existing.Update(
					AdjustmentName,
					desired.Amount,
					isEarning: false,
					AdjustmentReason,
					desired.Description);
				await payrollAdjustmentRepository.UpdateAsync(existing, autoSave: true);
				updatedSourceIds.Add(desired.SourceId);
			}
			else
			{
				await payrollAdjustmentRepository.DeleteAsync(existing, autoSave: true);
			}
		}

		foreach (var desired in desiredByEmployee.Values.Where(x => !updatedSourceIds.Contains(x.SourceId)))
		{
			var adjustment = new PayrollAdjustment(
				guidGenerator.Create(),
				currentTenant.Id,
				internalMonthYear,
				desired.EmployeeId,
				AdjustmentName,
				desired.Amount,
				isEarning: false,
				AdjustmentReason,
				desired.Description,
				PayrollAdjustmentSourceTypes.LateArrivalSalaryDeduction,
				desired.SourceId,
				isSystemGenerated: true);
			await payrollAdjustmentRepository.InsertAsync(adjustment, autoSave: true);
		}
	}

	private async Task<HashSet<Guid>> GetLockedEmployeeIdsAsync(
		string internalMonthYear,
		IReadOnlyCollection<Guid> employeeIds)
	{
		var query = await payrollRunItemSnapshotRepository.GetQueryableAsync();
		var rows = await asyncExecuter.ToListAsync(query.Where(x =>
			x.MonthYear == internalMonthYear &&
			x.IsLocked &&
			employeeIds.Contains(x.EmployeeId)));

		return rows.Select(x => x.EmployeeId).ToHashSet();
	}

	private async Task<List<PayrollAdjustment>> GetExistingAdjustmentsAsync(
		string internalMonthYear,
		IReadOnlyCollection<Guid> employeeIds)
	{
		var query = await payrollAdjustmentRepository.GetQueryableAsync();
		return await asyncExecuter.ToListAsync(query.Where(x =>
			x.MonthYear == internalMonthYear &&
			x.SourceType == PayrollAdjustmentSourceTypes.LateArrivalSalaryDeduction &&
			employeeIds.Contains(x.EmployeeId)));
	}

	private static DesiredLateArrivalDeduction? BuildDesiredAdjustment(
		string internalMonthYear,
		int year,
		int month,
		Employee employee,
		IReadOnlyCollection<EmployeeAttendance> attendanceRecords,
		AttendancePolicy policy)
	{
		if (policy.PenaltyType != AttendancePenaltyType.SalaryDeduction ||
		    !policy.PenaltyDeductionAmount.HasValue ||
		    policy.PenaltyDeductionAmount.Value <= 0m ||
		    employee.Shift == null)
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
				IsLateArrival(x.TimeIn.Value, employee.Shift, policy.GraceTime))
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

		return new DesiredLateArrivalDeduction(
			employee.Id,
			amount,
			description,
			CreateSourceId(internalMonthYear, employee.Id));
	}

	private static bool IsLateArrival(DateTimeOffset timeIn, ShiftManagement shift, int graceTime) =>
		(timeIn.TimeOfDay - shift.StartTime.ToTimeSpan()).TotalMinutes > graceTime;

	private static Guid CreateSourceId(string internalMonthYear, Guid employeeId)
	{
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
			$"{PayrollAdjustmentSourceTypes.LateArrivalSalaryDeduction}|{internalMonthYear}|{employeeId:D}"));
		return new Guid(hash.Take(16).ToArray());
	}

	private sealed record DesiredLateArrivalDeduction(
		Guid EmployeeId,
		decimal Amount,
		string Description,
		Guid SourceId);
}

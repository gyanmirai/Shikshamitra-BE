using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollSnapshotAdjustmentInvalidationReader(
	PayrollAdjustmentReader adjustmentReader,
	ILogger<PayrollSnapshotAdjustmentInvalidationReader> logger) : ITransientDependency
{
	public async Task<bool> HasAdjustmentInvalidationsAsync(
		string monthYear,
		IReadOnlyCollection<Guid> employeeIds,
		IReadOnlyDictionary<Guid, PayrollRunItemSnapshot> snapshotByEmployee)
	{
		var adjustmentsByEmployee = await adjustmentReader.GetByEmployeeAsync(monthYear, employeeIds);
		foreach (var snapshot in snapshotByEmployee.Values.Where(x => !x.IsLocked))
		{
			if (SnapshotAdjustmentsChanged(snapshot, adjustmentsByEmployee))
			{
				logger.LogDebug(
					"Payroll snapshot for employee {EmployeeId} in {MonthYear} is stale after adjustment change.",
					snapshot.EmployeeId,
					snapshot.MonthYear);
				return true;
			}
		}

		return false;
	}

	private static bool SnapshotAdjustmentsChanged(
		PayrollRunItemSnapshot snapshot,
		IReadOnlyDictionary<Guid, List<PayrollAdjustment>> adjustmentsByEmployee)
	{
		var currentAdjustmentsJson = JsonSerializer.Serialize(
			adjustmentsByEmployee.GetValueOrDefault(snapshot.EmployeeId)?.Select(ToAdjustmentFingerprint).ToList()
			?? new List<PayrollAdjustmentFingerprint>());
		var snapshotAdjustmentsJson = JsonSerializer.Serialize(
			(JsonSerializer.Deserialize<List<PayrollAdjustmentDto>>(snapshot.PayrollAdjustmentsJson)
			 ?? new List<PayrollAdjustmentDto>()).Select(ToAdjustmentFingerprint).ToList());
		return snapshotAdjustmentsJson != currentAdjustmentsJson;
	}

	private static PayrollAdjustmentFingerprint ToAdjustmentFingerprint(PayrollAdjustment adjustment) =>
		new(
			adjustment.Id,
			adjustment.MonthYear,
			adjustment.EmployeeId,
			adjustment.Name,
			adjustment.Amount,
			adjustment.IsEarning,
			adjustment.Reason,
			adjustment.Description);

	private static PayrollAdjustmentFingerprint ToAdjustmentFingerprint(PayrollAdjustmentDto adjustment) =>
		new(
			adjustment.Id,
			adjustment.MonthYear,
			adjustment.EmployeeId,
			adjustment.Name,
			(decimal)adjustment.Amount,
			adjustment.IsEarning,
			adjustment.Reason,
			adjustment.Description);

	private readonly record struct PayrollAdjustmentFingerprint(
		Guid Id,
		string MonthYear,
		Guid EmployeeId,
		string Name,
		decimal Amount,
		bool IsEarning,
		string Reason,
		string? Description);
}

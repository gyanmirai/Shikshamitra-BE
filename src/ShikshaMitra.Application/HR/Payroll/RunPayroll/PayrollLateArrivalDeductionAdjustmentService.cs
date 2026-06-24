using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings;
using Volo.Abp;
using Volo.Abp.Data;
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
	IDataFilter dataFilter,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task SyncAsync(
		string internalMonthYear,
		int year,
		int month,
		IReadOnlyCollection<Employee> employees,
		IReadOnlyDictionary<Guid, List<EmployeeAttendance>> attendanceByEmployee,
		IReadOnlyDictionary<Guid, ShiftManagement> shiftsById,
		IReadOnlyDictionary<Guid, HrResolvedPolicies> policiesByEmployee)
	{
		var employeeIds = employees.Select(x => x.Id).Distinct().ToList();
		if (employeeIds.Count == 0)
		{
			return;
		}

		var lockedEmployeeIds = await GetLockedEmployeeIdsAsync(internalMonthYear, employeeIds);
		var existingAdjustments = await GetExistingAdjustmentsAsync(internalMonthYear, employeeIds);
		var desiredByEmployee = new Dictionary<Guid, PayrollLateArrivalDeductionCalculation>();

		foreach (var employee in employees)
		{
			if (lockedEmployeeIds.Contains(employee.Id) ||
			    !policiesByEmployee.TryGetValue(employee.Id, out var policies))
			{
				continue;
			}

			var desired = PayrollLateArrivalDeductionCalculator.Calculate(
				internalMonthYear,
				year,
				month,
				employee,
				attendanceByEmployee.TryGetValue(employee.Id, out var attendanceRecords)
					? attendanceRecords
					: [],
				ResolveShift(employee, shiftsById),
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
				existing.IsDeleted = false;
				existing.DeletionTime = null;
				existing.DeleterId = null;
				existing.UpdatePostingScope(internalMonthYear, existing.EmployeeId);
				existing.SetSource(
					PayrollAdjustmentSourceTypes.LateArrivalSalaryDeduction,
					desired.SourceId,
					isSystemGenerated: true);
				existing.Update(
					PayrollLateArrivalDeductionCalculator.AdjustmentName,
					desired.Amount,
					isEarning: false,
					PayrollLateArrivalDeductionCalculator.AdjustmentReason,
					desired.Description);
				await payrollAdjustmentRepository.UpdateAsync(existing, autoSave: true);
				updatedSourceIds.Add(desired.SourceId);
			}
			else
			{
				if (!existing.IsDeleted)
				{
					await payrollAdjustmentRepository.DeleteAsync(existing, autoSave: true);
				}
			}
		}

		foreach (var desired in desiredByEmployee.Values.Where(x => !updatedSourceIds.Contains(x.SourceId)))
		{
			var adjustment = new PayrollAdjustment(
				guidGenerator.Create(),
				currentTenant.Id,
				internalMonthYear,
				desired.EmployeeId,
				PayrollLateArrivalDeductionCalculator.AdjustmentName,
				desired.Amount,
				isEarning: false,
				PayrollLateArrivalDeductionCalculator.AdjustmentReason,
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
		using (dataFilter.Disable<ISoftDelete>())
		{
			var query = await payrollAdjustmentRepository.GetQueryableAsync();
			return await asyncExecuter.ToListAsync(query.Where(x =>
				x.MonthYear == internalMonthYear &&
				x.SourceType == PayrollAdjustmentSourceTypes.LateArrivalSalaryDeduction &&
				employeeIds.Contains(x.EmployeeId)));
		}
	}

	private static ShiftManagement? ResolveShift(
		Employee employee,
		IReadOnlyDictionary<Guid, ShiftManagement> shiftsById)
	{
		if (employee.Shift != null)
		{
			return employee.Shift;
		}

		return employee.ShiftId.HasValue && shiftsById.TryGetValue(employee.ShiftId.Value, out var shift)
			? shift
			: null;
	}
}

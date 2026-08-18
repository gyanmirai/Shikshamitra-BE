using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Payroll.SalaryStructure;

public class EmployeeSalaryStructurePersistenceWorkflow(
	IEmployeePayrollComponentRepository payrollComponentRepository,
	IEmployeePayrollDetailsRepository payrollDetailsRepository,
	PayrollSnapshotReviewInvalidator payrollSnapshotReviewInvalidator) : ITransientDependency
{
	public async Task SaveAsync(
		Guid employeeId,
		EmployeePayrollDetails payrollDetails,
		bool payrollDetailsExist,
		IReadOnlyList<EmployeePayrollComponent> components,
		bool salaryGenerationRefreshed)
	{
		if (payrollDetailsExist && !salaryGenerationRefreshed)
		{
			var existing = (await payrollComponentRepository.GetEarningsByEmployeeIdAsync(employeeId))
				.Concat(await payrollComponentRepository.GetDeductionsByEmployeeIdAsync(employeeId))
				.ToList();
			if (HaveRecurringComponentsChanged(existing, components))
			{
				payrollDetails.MarkSalaryComponentsModified();
			}
		}

		await payrollComponentRepository.DeleteByEmployeeIdAsync(employeeId, isEarning: true);
		await payrollComponentRepository.DeleteByEmployeeIdAsync(employeeId, isEarning: false);
		if (components.Count > 0)
		{
			await payrollComponentRepository.InsertManyAsync(components);
		}

		if (payrollDetailsExist)
		{
			await payrollDetailsRepository.UpdateAsync(payrollDetails);
		}
		else
		{
			await payrollDetailsRepository.InsertAsync(payrollDetails);
		}

		await payrollSnapshotReviewInvalidator.InvalidateUnlockedForEmployeesAsync([employeeId]);
	}

	private static bool HaveRecurringComponentsChanged(
		IEnumerable<EmployeePayrollComponent> existing,
		IEnumerable<EmployeePayrollComponent> replacement)
	{
		var existingRecurring = OrderForComparison(existing.Where(x => !x.IsTemp)).ToList();
		var replacementRecurring = OrderForComparison(replacement.Where(x => !x.IsTemp)).ToList();
		if (existingRecurring.Count != replacementRecurring.Count)
		{
			return true;
		}

		return existingRecurring.Zip(replacementRecurring).Any(pair =>
			pair.First.ComponentId != pair.Second.ComponentId ||
			!string.Equals(pair.First.Name, pair.Second.Name, StringComparison.Ordinal) ||
			pair.First.IsEarning != pair.Second.IsEarning ||
			pair.First.Amount != pair.Second.Amount ||
			pair.First.SemanticType != pair.Second.SemanticType ||
			pair.First.StatutoryRuleId != pair.Second.StatutoryRuleId);
	}

	private static IOrderedEnumerable<EmployeePayrollComponent> OrderForComparison(
		IEnumerable<EmployeePayrollComponent> components) =>
		components
			.OrderBy(x => x.IsEarning)
			.ThenBy(x => x.ComponentId)
			.ThenBy(x => x.Name, StringComparer.Ordinal);
}

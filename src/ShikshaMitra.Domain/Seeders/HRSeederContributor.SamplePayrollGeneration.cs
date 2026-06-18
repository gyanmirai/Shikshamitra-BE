using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Settings;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	private async Task SeedSamplePayrollForEmployeeIfNeededAsync(Guid employeeId, string? pfRuleId)
	{
		var hasPendingChanges = false;
		var employee = await _employeeRepository.FindAsync(employeeId);
		if (employee == null)
		{
			return;
		}

		var forceRepairSeededDemoPayroll = IsSeededDemoEmployee(employee);
		var existingComponents = await _employeePayrollComponentRepository.GetByEmployeeIdAsync(employeeId);
		var existingComponentsById = existingComponents
			.Where(x => x.ComponentId.HasValue)
			.GroupBy(x => (x.ComponentId!.Value, x.IsEarning))
			.ToDictionary(x => x.Key, x => x.First());
		var seededSalary = BuildSeededSalary(employee);
		var financialData = BuildSeedEmployeeFinancialData(employee);

		var payrollDetails = await _employeePayrollDetailsRepository.GetByEmployeeIdAsync(employeeId);
		if (payrollDetails == null)
		{
			var details = new EmployeePayrollDetails(GuidGenerator.Create(), employeeId);
			details.UpdateDetails(
				bankName: financialData.BankName,
				branchName: financialData.BranchName,
				accountHolderName: financialData.AccountHolderName,
				accountNumber: financialData.AccountNumber,
				ifscCode: financialData.IfscCode,
				pfRule: pfRuleId,
				epfNumber: financialData.EpfNumber,
				uanNumber: financialData.UanNumber,
				punchId: financialData.PunchId,
				totalWorkingHourPerMonth: financialData.TotalWorkingHourPerMonth);
			await _employeePayrollDetailsRepository.InsertAsync(details, autoSave: false);
			hasPendingChanges = true;
		}
		else if (forceRepairSeededDemoPayroll && ShouldRepairSeedPayrollDetails(
			         financialData,
			         pfRuleId,
			         payrollDetails.BankName,
			         payrollDetails.BranchName,
			         payrollDetails.AccountHolderName,
			         payrollDetails.AccountNumber,
			         payrollDetails.IfscCode,
			         payrollDetails.PfRule,
			         payrollDetails.EpfNumber,
			         payrollDetails.UanNumber,
			         payrollDetails.PunchId,
			         payrollDetails.TotalWorkingHourPerMonth))
		{
			payrollDetails.UpdateDetails(
				bankName: financialData.BankName,
				branchName: financialData.BranchName,
				accountHolderName: financialData.AccountHolderName,
				accountNumber: financialData.AccountNumber,
				ifscCode: financialData.IfscCode,
				pfRule: pfRuleId,
				epfNumber: financialData.EpfNumber,
				uanNumber: financialData.UanNumber,
				punchId: financialData.PunchId,
				totalWorkingHourPerMonth: financialData.TotalWorkingHourPerMonth);
			await _employeePayrollDetailsRepository.UpdateAsync(payrollDetails, autoSave: false);
			hasPendingChanges = true;
		}

		var earningMasters = (await _earningComponentRepository.GetListAsync())
			.Where(e => e.IsActive)
			.OrderBy(e => e.DisplayOrder)
			.ToList();
		var deductionMasters = (await _deductionComponentRepository.GetListAsync())
			.Where(d => d.IsActive)
			.OrderBy(d => d.DisplayOrder)
			.ToList();

		foreach (var master in earningMasters)
		{
			var amount = seededSalary.GetEarningAmount(master.Name);
			var isFixed = master.CalculationType == CalculationType.Amount;
			if (existingComponentsById.TryGetValue((master.Id, true), out var existing))
			{
				if (amount <= 0m)
				{
					await _employeePayrollComponentRepository.DeleteAsync(existing, autoSave: false);
					hasPendingChanges = true;
					continue;
				}

				if (forceRepairSeededDemoPayroll || existing.UsesMasterValue || existing.Amount == master.Amount)
				{
					existing.UpdateComponent(
						master.Name,
						master.Description,
						amount,
						isFixed,
						isTemp: false,
						master.CalculationType == CalculationType.Amount
							? SalaryComponentType.Fixed
							: SalaryComponentType.Percentage,
						MapSalaryDependency(master.DependOn),
						usesMasterValue: false);
					await _employeePayrollComponentRepository.UpdateAsync(existing, autoSave: false);
					hasPendingChanges = true;
				}

				continue;
			}

			if (amount <= 0m)
			{
				continue;
			}

			var row = new EmployeePayrollComponent(
				GuidGenerator.Create(),
				employeeId,
				master.Name,
				master.Id,
				master.Description,
				amount,
				isFixed,
				isTemp: false,
				applicableMonth: null,
				master.CalculationType == CalculationType.Amount
					? SalaryComponentType.Fixed
					: SalaryComponentType.Percentage,
				MapSalaryDependency(master.DependOn),
				isEarning: true,
				usesMasterValue: false);
			await _employeePayrollComponentRepository.InsertAsync(row, autoSave: false);
			hasPendingChanges = true;
		}

		foreach (var master in deductionMasters)
		{
			var amount = seededSalary.GetDeductionAmount(master.Name);
			var isFixed = master.CalculationType == CalculationType.Amount;
			if (existingComponentsById.TryGetValue((master.Id, false), out var existing))
			{
				if (amount <= 0m)
				{
					await _employeePayrollComponentRepository.DeleteAsync(existing, autoSave: false);
					hasPendingChanges = true;
					continue;
				}

				if (forceRepairSeededDemoPayroll || existing.UsesMasterValue || existing.Amount == master.Amount)
				{
					existing.UpdateComponent(
						master.Name,
						master.Description,
						amount,
						isFixed,
						isTemp: false,
						master.CalculationType == CalculationType.Amount
							? SalaryComponentType.Fixed
							: SalaryComponentType.Percentage,
						MapSalaryDependency(master.DependOn),
						usesMasterValue: false);
					await _employeePayrollComponentRepository.UpdateAsync(existing, autoSave: false);
					hasPendingChanges = true;
				}

				continue;
			}

			if (amount <= 0m)
			{
				continue;
			}

			var row = new EmployeePayrollComponent(
				GuidGenerator.Create(),
				employeeId,
				master.Name,
				master.Id,
				master.Description,
				amount,
				isFixed,
				isTemp: false,
				applicableMonth: null,
				master.CalculationType == CalculationType.Amount
					? SalaryComponentType.Fixed
					: SalaryComponentType.Percentage,
				MapSalaryDependency(master.DependOn),
				isEarning: false,
				usesMasterValue: false);
			await _employeePayrollComponentRepository.InsertAsync(row, autoSave: false);
			hasPendingChanges = true;
		}

		if (hasPendingChanges)
		{
			await InvalidateUnlockedPayrollSnapshotsAsync(employeeId);
			await CurrentUnitOfWork.SaveChangesAsync();
		}
	}
}

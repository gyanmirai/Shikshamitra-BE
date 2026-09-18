using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.Templates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
		var existingComponentsByName = existingComponents
			.Where(x => !string.IsNullOrWhiteSpace(x.Name) && !x.IsTemp)
			.GroupBy(x => (NormalizeSeedPayrollComponentName(x.Name), x.IsEarning))
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
			var isAdditional = IsAdditionalSeedEarning(master.Name);
			var existing = FindExistingSeedPayrollComponent(
				existingComponentsById,
				existingComponentsByName,
				master.Id,
				master.Name,
				isEarning: true);
			if (existing != null)
			{
				if (amount <= 0m)
				{
					await _employeePayrollComponentRepository.DeleteAsync(existing, autoSave: false);
					hasPendingChanges = true;
					continue;
				}

				if (forceRepairSeededDemoPayroll || existing.UsesMasterValue || existing.Amount == master.Amount)
				{
					existing.ComponentId = master.Id;
					existing.ApplicableMonth = isAdditional ? new DateTime(_clock.Now.Year, _clock.Now.Month, 1) : null;
					existing.UpdateComponent(
						master.Name,
						master.Description,
						amount,
						isFixed,
						isTemp: isAdditional,
						master.CalculationType == CalculationType.Amount
							? SalaryComponentType.Fixed
							: SalaryComponentType.Percentage,
						MapSalaryDependency(master.DependOn),
						usesMasterValue: false);
					await _employeePayrollComponentRepository.UpdateAsync(existing, autoSave: false);
					hasPendingChanges = true;
				}

				if (forceRepairSeededDemoPayroll)
				{
					hasPendingChanges |= await DeleteDuplicateSeedPayrollRowsAsync(
						existingComponents,
						existing.Id,
						master.Id,
						master.Name,
						isEarning: true);
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
				isTemp: isAdditional,
				applicableMonth: isAdditional ? new DateTime(_clock.Now.Year, _clock.Now.Month, 1) : null,
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
			var existing = FindExistingSeedPayrollComponent(
				existingComponentsById,
				existingComponentsByName,
				master.Id,
				master.Name,
				isEarning: false);
			if (existing != null)
			{
				if (amount <= 0m)
				{
					await _employeePayrollComponentRepository.DeleteAsync(existing, autoSave: false);
					hasPendingChanges = true;
					continue;
				}

				if (forceRepairSeededDemoPayroll || existing.UsesMasterValue || existing.Amount == master.Amount)
				{
					existing.ComponentId = master.Id;
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

				if (forceRepairSeededDemoPayroll)
				{
					hasPendingChanges |= await DeleteDuplicateSeedPayrollRowsAsync(
						existingComponents,
						existing.Id,
						master.Id,
						master.Name,
						isEarning: false);
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

		hasPendingChanges |= await EnsureSeededCompensationRevisionAsync(
			employee,
			seededSalary,
			earningMasters,
			deductionMasters,
			pfRuleId);

		if (hasPendingChanges)
		{
			await InvalidateUnlockedPayrollSnapshotsAsync(employeeId);
			await CurrentUnitOfWork.SaveChangesAsync();
		}
	}

	private async Task<bool> EnsureSeededCompensationRevisionAsync(
		Employee employee,
		SeededSalaryProfile seededSalary,
		IReadOnlyList<EarningComponent> earningMasters,
		IReadOnlyList<DeductionComponent> deductionMasters,
		string? pfRuleId)
	{
		if ((await _compensationRevisionRepository.GetListAsync(x => x.EmployeeId == employee.Id)).Count > 0)
		{
			return false;
		}

		var template = (await _salaryStructureTemplateRepository.GetListAsync())
			.FirstOrDefault(x => x.IsDefault && x.PublishedVersion.HasValue);
		var templateVersion = template == null
			? null
			: (await _salaryStructureTemplateVersionRepository.GetListAsync(x => x.TemplateId == template.Id))
				.FirstOrDefault(x => x.VersionNumber == template.PublishedVersion);
		var employerProvidentFund = seededSalary.GetDeductionAmount("Provident Fund (PF)");
		var employerCost = seededSalary.GrossSalary + employerProvidentFund;
		var deductions = seededSalary.Deductions.Values.Sum();
		var revision = new EmployeeCompensationRevision(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			employee.Id,
			1,
			new DateTime(2022, 4, 1, 0, 0, 0, DateTimeKind.Utc));
		var snapshot = JsonSerializer.Serialize(new
		{
			Gross = seededSalary.GrossSalary,
			Earnings = earningMasters.Where(x => !IsAdditionalSeedEarning(x.Name) && seededSalary.GetEarningAmount(x.Name) > 0m).Select(x => new
			{
				ComponentId = x.Id,
				x.Name,
				Amount = seededSalary.GetEarningAmount(x.Name),
				FormulaType = "FIXED_AMOUNT",
				FormulaValue = seededSalary.GetEarningAmount(x.Name),
				SemanticType = PayrollComponentSemanticTypeCodes.ToCode(ResolveSeededSemanticType(x.Name))
			}),
			Deductions = new object[]
			{
				new
				{
					ComponentId = deductionMasters.First(x => x.Name == "Provident Fund (PF)").Id,
					Name = "Provident Fund (PF)",
					IsStatutory = true,
					SemanticType = "EMPLOYEE_PROVIDENT_FUND"
				},
				new
				{
					ComponentId = deductionMasters.First(x => x.Name == "Professional Tax").Id,
					Name = "Professional Tax",
					IsStatutory = true,
					SemanticType = "PROFESSIONAL_TAX"
				}
			},
			PfRuleId = Guid.TryParse(pfRuleId, out var parsedPfRuleId) ? parsedPfRuleId : (Guid?)null
		});
		revision.ReplaceDraft(
			revision.EffectiveMonth,
			SalaryInputBasis.EmployerCost,
			CompensationProtectionPolicy.ProtectEmployerCost,
			employerCost,
			employerCost,
			seededSalary.GrossSalary,
			seededSalary.GrossSalary - deductions,
			employerCost,
			template?.Id,
			templateVersion?.Id,
			templateVersion?.VersionNumber,
			Guid.TryParse(pfRuleId, out parsedPfRuleId) ? parsedPfRuleId : null,
			"IN-GJ",
			snapshot);
		var submitterId = GuidGenerator.Create();
		revision.Submit(submitterId, new DateTime(2022, 4, 1, 0, 0, 0, DateTimeKind.Utc));
		revision.Approve(GuidGenerator.Create(), new DateTime(2022, 4, 1, 0, 1, 0, DateTimeKind.Utc));
		await _compensationRevisionRepository.InsertAsync(revision, autoSave: false);

		var lines = new List<EmployeeCompensationRevisionLine>();
		foreach (var master in earningMasters.Where(x => !IsAdditionalSeedEarning(x.Name)))
		{
			var amount = seededSalary.GetEarningAmount(master.Name);
			if (amount <= 0m)
			{
				continue;
			}

			lines.Add(new EmployeeCompensationRevisionLine(
				GuidGenerator.Create(), CurrentTenant.Id, revision.Id, employee.Id, master.Id,
				master.Name, master.Description, amount, isEarning: true,
				ResolveSeededSemanticType(master.Name), CompensationLineSource.Template, lines.Count));
		}

		foreach (var master in deductionMasters.Where(x => x.Name is not "Provident Fund (PF)" and not "Professional Tax"))
		{
			var amount = seededSalary.GetDeductionAmount(master.Name);
			if (amount <= 0m)
			{
				continue;
			}

			lines.Add(new EmployeeCompensationRevisionLine(
				GuidGenerator.Create(), CurrentTenant.Id, revision.Id, employee.Id, master.Id,
				master.Name, master.Description, amount, isEarning: false,
				PayrollComponentSemanticType.Ordinary, CompensationLineSource.Template, lines.Count));
		}

		await _compensationRevisionLineRepository.InsertManyAsync(lines, autoSave: false);
		return true;
	}

	private static bool IsAdditionalSeedEarning(string name) => name is "Performance Bonus" or "Incentive";

	private static PayrollComponentSemanticType ResolveSeededSemanticType(string componentName) =>
		componentName switch
		{
			"Basic Salary" => PayrollComponentSemanticType.Basic,
			"Dearness Allowance (DA)" => PayrollComponentSemanticType.DearnessAllowance,
			"Special Allowance" => PayrollComponentSemanticType.BalancingEarning,
			_ => PayrollComponentSemanticType.Ordinary
		};

	internal static EmployeePayrollComponent? FindExistingSeedPayrollComponent(
		IReadOnlyDictionary<(Guid ComponentId, bool IsEarning), EmployeePayrollComponent> existingComponentsById,
		IReadOnlyDictionary<(string Name, bool IsEarning), EmployeePayrollComponent> existingComponentsByName,
		Guid componentId,
		string componentName,
		bool isEarning)
	{
		if (existingComponentsById.TryGetValue((componentId, isEarning), out var byId))
		{
			return byId;
		}

		return existingComponentsByName.TryGetValue(
			(NormalizeSeedPayrollComponentName(componentName), isEarning),
			out var byName)
			? byName
			: null;
	}

	private async Task<bool> DeleteDuplicateSeedPayrollRowsAsync(
		IEnumerable<EmployeePayrollComponent> existingComponents,
		Guid keepId,
		Guid componentId,
		string componentName,
		bool isEarning)
	{
		var normalizedName = NormalizeSeedPayrollComponentName(componentName);
		var duplicates = existingComponents
			.Where(x =>
				x.Id != keepId &&
				x.IsEarning == isEarning &&
				!x.IsTemp &&
				((x.ComponentId.HasValue && x.ComponentId.Value == componentId) ||
				 NormalizeSeedPayrollComponentName(x.Name) == normalizedName))
			.ToList();

		foreach (var duplicate in duplicates)
		{
			await _employeePayrollComponentRepository.DeleteAsync(duplicate, autoSave: false);
		}

		return duplicates.Count > 0;
	}

	private static string NormalizeSeedPayrollComponentName(string? name) =>
		(name ?? string.Empty).Trim().ToUpperInvariant();
}

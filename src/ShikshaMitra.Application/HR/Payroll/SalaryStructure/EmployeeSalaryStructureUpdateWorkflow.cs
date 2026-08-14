using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Payroll.SalaryStructure;

public class EmployeeSalaryStructureUpdateWorkflow(
	IEmployeePayrollComponentRepository payrollComponentRepository,
	IEmployeePayrollDetailsRepository payrollDetailsRepository,
	SalaryStructureMasterResolver masterResolver,
	SalaryStructureComponentFactory componentFactory,
	EmployeeSalaryStructurePersistenceWorkflow persistenceWorkflow,
	Volo.Abp.Guids.IGuidGenerator guidGenerator) : ITransientDependency
{
	public async Task UpdateAsync(Guid employeeId, UpdateSalaryStructureDto input)
	{
		var existingEarnings = await payrollComponentRepository.GetEarningsByEmployeeIdAsync(employeeId);
		var existingDeductions = await payrollComponentRepository.GetDeductionsByEmployeeIdAsync(employeeId);
		var existingEarningById = existingEarnings.ToDictionary(x => x.Id);
		var existingDeductionById = existingDeductions.ToDictionary(x => x.Id);

		SalaryStructureComponentRules.EnsureCustomComponentIdsAreValid(
			input.EarningComponents,
			existingEarningById,
			HRErrorCodes.EmployeePayrollInvalidEarningComponent);
		SalaryStructureComponentRules.EnsureCustomComponentIdsAreValid(
			input.DeductionComponents,
			existingDeductionById,
			HRErrorCodes.EmployeePayrollInvalidDeductionComponent);

		var earningComponentsWithMaster = input.EarningComponents.Where(SalaryStructureComponentRules.ShouldResolveMasterComponent).ToList();
		var deductionComponentsWithMaster = input.DeductionComponents.Where(SalaryStructureComponentRules.ShouldResolveMasterComponent).ToList();
		var earningMasterById = await masterResolver.GetEarningMasterByIdAsync(earningComponentsWithMaster);
		var deductionMasterById = await masterResolver.GetDeductionMasterByIdAsync(deductionComponentsWithMaster);
		SalaryStructureComponentRules.EnsureAllMasterComponentsExist(
			earningComponentsWithMaster.Select(x => x.ComponentId!.Value),
			earningMasterById.Keys,
			HRErrorCodes.EmployeePayrollInvalidEarningComponent);
		SalaryStructureComponentRules.EnsureAllMasterComponentsExist(
			deductionComponentsWithMaster.Select(x => x.ComponentId!.Value),
			deductionMasterById.Keys,
			HRErrorCodes.EmployeePayrollInvalidDeductionComponent);

		var resolvedEarnings = input.EarningComponents
			.Select(earning =>
			{
				var master = SalaryStructureComponentRules.ShouldResolveMasterComponent(earning)
					&& earningMasterById.TryGetValue(earning.ComponentId!.Value, out var earningMaster)
					? earningMaster
					: null;
				return new ResolvedSalaryComponent<EarningComponent>(
					earning,
					SalaryStructureComponentRules.FindExistingComponent(earning, existingEarningById),
					master);
			})
			.ToList();
		var resolvedDeductions = input.DeductionComponents
			.Select(deduction =>
			{
				var master = SalaryStructureComponentRules.ShouldResolveMasterComponent(deduction)
					&& deductionMasterById.TryGetValue(deduction.ComponentId!.Value, out var deductionMaster)
					? deductionMaster
					: null;
				return new ResolvedSalaryComponent<DeductionComponent>(
					deduction,
					SalaryStructureComponentRules.FindExistingComponent(deduction, existingDeductionById),
					master);
			})
			.ToList();

		var earningAmountLines = resolvedEarnings.Select(SalaryStructureComponentRules.ToAmountLine).ToList();
		var deductionAmountLines = resolvedDeductions.Select(SalaryStructureComponentRules.ToAmountLine).ToList();
		var earningAmountsByComponentId = PayrollComponentAmountResolver.ResolveEarningAmounts(earningAmountLines);
		var grossAmount = earningAmountsByComponentId.Values.Sum();
		var deductionAmountsByComponentId = PayrollComponentAmountResolver.ResolveDeductionAmounts(
			deductionAmountLines,
			earningAmountLines,
			earningAmountsByComponentId,
			grossAmount);

		var componentsToInsert = new List<EmployeePayrollComponent>();
		foreach (var resolved in resolvedEarnings)
		{
			componentsToInsert.Add(componentFactory.BuildEarningComponent(employeeId, resolved, earningAmountsByComponentId));
		}

		foreach (var resolved in resolvedDeductions)
		{
			componentsToInsert.Add(componentFactory.BuildDeductionComponent(employeeId, resolved, deductionAmountsByComponentId));
		}

		var existingDetails = await payrollDetailsRepository.GetByEmployeeIdAsync(employeeId);
		var salaryGenerationRefreshed =
			input.SourceSalaryTemplateVersionId.HasValue &&
			input.SalaryGeneratedAt.HasValue &&
			(existingDetails == null ||
			 existingDetails.SourceSalaryTemplateVersionId != input.SourceSalaryTemplateVersionId ||
			 existingDetails.SalaryGeneratedAt != input.SalaryGeneratedAt);
		var details = existingDetails ?? new EmployeePayrollDetails(guidGenerator.Create(), employeeId);
		details.UpdateDetails(
			details.BankName,
			details.BranchName,
			details.AccountHolderName,
			details.AccountNumber,
			details.IfscCode,
			input.PfRuleId?.ToString(),
			details.EpfNumber,
			details.UanNumber,
			details.PunchId,
			details.TotalWorkingHourPerMonth);
		details.SetSalaryGeneration(
			input.QuotedSalaryBasis,
			input.QuotedMonthlyAmount,
			input.SourceSalaryTemplateId,
			input.SourceSalaryTemplateVersionId,
			input.SourceSalaryTemplateVersion,
			input.SalaryGeneratedAt,
			input.WasModifiedAfterGeneration);
		await persistenceWorkflow.SaveAsync(
			employeeId, details, existingDetails != null, componentsToInsert, salaryGenerationRefreshed);
	}
}

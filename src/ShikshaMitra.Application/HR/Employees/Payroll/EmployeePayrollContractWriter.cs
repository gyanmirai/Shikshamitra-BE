using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.SalaryStructure;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;

namespace ShikshaMitra.HR.Employees.Payroll;

public class EmployeePayrollContractWriter(
	IEmployeePayrollDetailsRepository payrollDetailsRepository,
	EmployeeSalaryStructurePersistenceWorkflow persistenceWorkflow,
	IGuidGenerator guidGenerator) : ITransientDependency
{
	public async Task SaveAsync(
		Guid employeeId,
		PayrollContract input,
		IEnumerable<EmployeePayrollResolvedLine<EarningComponent>> earningLines,
		IEnumerable<EmployeePayrollResolvedLine<DeductionComponent>> deductionLines)
	{
		var resolvedEarnings = earningLines.ToList();
		var resolvedDeductions = deductionLines.ToList();
		var amountPlan = EmployeePayrollComponentAmountPlanner.Create(resolvedEarnings, resolvedDeductions);
		var (payrollDetails, payrollDetailsExist, salaryGenerationRefreshed) = await PreparePayrollDetailsAsync(employeeId, input);
		var components = BuildPayrollComponents(employeeId, resolvedEarnings, resolvedDeductions, amountPlan);
		await persistenceWorkflow.SaveAsync(
			employeeId, payrollDetails, payrollDetailsExist, components, salaryGenerationRefreshed);
	}

	private async Task<(EmployeePayrollDetails Details, bool Exists, bool SalaryGenerationRefreshed)> PreparePayrollDetailsAsync(
		Guid employeeId,
		PayrollContract input)
	{
		var payrollDetails = await payrollDetailsRepository.GetByEmployeeIdAsync(employeeId);
		if (payrollDetails == null)
		{
			payrollDetails = new EmployeePayrollDetails(guidGenerator.Create(), employeeId);
			payrollDetails.UpdateDetails(
				input.BankDetails?.BankName,
				input.BankDetails?.BranchName,
				input.BankDetails?.AccountHolderName,
				input.BankDetails?.AccountNumber,
				input.BankDetails?.IfscCode,
				input.SalaryDetails?.PfRuleId?.ToString(),
				null,
				null,
				input.SalaryDetails?.PunchId,
				input.SalaryDetails?.TotalWorkingHourPerMonth);
			ApplySalaryGeneration(payrollDetails, input.SalaryDetails);
			return (payrollDetails, false, input.SalaryDetails?.SourceSalaryTemplateVersionId.HasValue == true);
		}

		var bank = input.BankDetails;
		var salary = input.SalaryDetails;
		payrollDetails.UpdateDetails(
			bank != null ? bank.BankName : payrollDetails.BankName,
			bank != null ? bank.BranchName : payrollDetails.BranchName,
			bank != null ? bank.AccountHolderName : payrollDetails.AccountHolderName,
			bank != null ? bank.AccountNumber : payrollDetails.AccountNumber,
			bank != null ? bank.IfscCode : payrollDetails.IfscCode,
			salary != null ? salary.PfRuleId?.ToString() : payrollDetails.PfRule,
			payrollDetails.EpfNumber,
			payrollDetails.UanNumber,
			salary != null ? salary.PunchId : payrollDetails.PunchId,
			salary != null ? salary.TotalWorkingHourPerMonth : payrollDetails.TotalWorkingHourPerMonth);
		var salaryGenerationRefreshed = IsSalaryGenerationRefreshed(payrollDetails, salary);
		ApplySalaryGeneration(payrollDetails, salary);
		return (payrollDetails, true, salaryGenerationRefreshed);
	}

	private static bool IsSalaryGenerationRefreshed(
		EmployeePayrollDetails existing,
		SalaryDetailsContract? salary) =>
		salary?.SourceSalaryTemplateVersionId.HasValue == true &&
		salary.SalaryGeneratedAt.HasValue &&
		(existing.SourceSalaryTemplateVersionId != salary.SourceSalaryTemplateVersionId ||
		 existing.SalaryGeneratedAt != salary.SalaryGeneratedAt);

	private static void ApplySalaryGeneration(EmployeePayrollDetails payrollDetails, SalaryDetailsContract? salary)
	{
		if (salary == null)
		{
			return;
		}

		payrollDetails.SetSalaryGeneration(
			salary.QuotedSalaryBasis,
			salary.QuotedMonthlyAmount,
			salary.SourceSalaryTemplateId,
			salary.SourceSalaryTemplateVersionId,
			salary.SourceSalaryTemplateVersion,
			salary.SalaryGeneratedAt,
			salary.WasModifiedAfterGeneration);
	}

	private List<EmployeePayrollComponent> BuildPayrollComponents(
		Guid employeeId,
		IEnumerable<EmployeePayrollResolvedLine<EarningComponent>> earningLines,
		IEnumerable<EmployeePayrollResolvedLine<DeductionComponent>> deductionLines,
		EmployeePayrollComponentAmountPlan amountPlan)
	{
		var components = new List<EmployeePayrollComponent>();
		foreach (var resolved in earningLines)
		{
			components.Add(BuildEarningComponent(employeeId, resolved, amountPlan));
		}

		foreach (var resolved in deductionLines)
		{
			components.Add(BuildDeductionComponent(employeeId, resolved, amountPlan));
		}

		return components;
	}

	private EmployeePayrollComponent BuildEarningComponent(
		Guid employeeId,
		EmployeePayrollResolvedLine<EarningComponent> resolved,
		EmployeePayrollComponentAmountPlan amountPlan)
	{
		var line = resolved.Line;
		var master = resolved.Master;
		var amount = EmployeePayrollComponentAmountPlanner.ResolveEarningLineAmount(
			resolved,
			amountPlan.EarningAmountsByComponentId)!.Value;
		var isFixed = line.UsesMasterValue == false
			? true
			: master is not null
			? master.CalculationType == CalculationType.Amount
			: line.IsFixed ?? resolved.ExistingPayrollComponent?.IsFixed ?? false;

		return new EmployeePayrollComponent(
			guidGenerator.Create(),
			employeeId,
			master?.Name ?? line.Name.Trim(),
			master?.Id,
			master?.Description ?? resolved.ExistingPayrollComponent?.Description,
			amount,
			isFixed,
			isTemp: line.IsTemp ?? resolved.ExistingPayrollComponent?.IsTemp ?? master is null,
			applicableMonth: line.ApplicableMonth ?? resolved.ExistingPayrollComponent?.ApplicableMonth,
			line.UsesMasterValue == false
				? SalaryComponentType.Fixed
				: master is null
				? resolved.ExistingPayrollComponent?.ComponentType ?? SalaryComponentType.Fixed
				: master.CalculationType == CalculationType.Amount
				? SalaryComponentType.Fixed
				: SalaryComponentType.Percentage,
			line.UsesMasterValue == false
				? SalaryComponentDependency.None
				: master is null ? resolved.ExistingPayrollComponent?.DependOn : PayrollComponentImpactProjection.MapDependency(master.DependOn),
			isEarning: true,
			usesMasterValue: EmployeePayrollComponentAmountPlanner.ShouldUseMasterValue(resolved, amount),
			semanticType: line.SemanticType,
			statutoryRuleId: line.StatutoryRuleId);
	}

	private EmployeePayrollComponent BuildDeductionComponent(
		Guid employeeId,
		EmployeePayrollResolvedLine<DeductionComponent> resolved,
		EmployeePayrollComponentAmountPlan amountPlan)
	{
		var line = resolved.Line;
		var master = resolved.Master;
		var amount = EmployeePayrollComponentAmountPlanner.ResolveDeductionLineAmount(
			resolved,
			amountPlan.DeductionAmountsByComponentId)!.Value;
		var isFixed = line.UsesMasterValue == false
			? true
			: master is not null
			? master.CalculationType == CalculationType.Amount
			: line.IsFixed ?? resolved.ExistingPayrollComponent?.IsFixed ?? false;

		return new EmployeePayrollComponent(
			guidGenerator.Create(),
			employeeId,
			master?.Name ?? line.Name.Trim(),
			master?.Id,
			master?.Description ?? resolved.ExistingPayrollComponent?.Description,
			amount,
			isFixed,
			isTemp: line.IsTemp ?? resolved.ExistingPayrollComponent?.IsTemp ?? master is null,
			applicableMonth: line.ApplicableMonth ?? resolved.ExistingPayrollComponent?.ApplicableMonth,
			line.UsesMasterValue == false
				? SalaryComponentType.Fixed
				: master is null
				? resolved.ExistingPayrollComponent?.ComponentType ?? SalaryComponentType.Fixed
				: master.CalculationType == CalculationType.Amount
				? SalaryComponentType.Fixed
				: SalaryComponentType.Percentage,
			line.UsesMasterValue == false
				? SalaryComponentDependency.None
				: master is null ? resolved.ExistingPayrollComponent?.DependOn : PayrollComponentImpactProjection.MapDependency(master.DependOn),
			isEarning: false,
			usesMasterValue: EmployeePayrollComponentAmountPlanner.ShouldUseMasterValue(resolved, amount),
			semanticType: line.SemanticType,
			statutoryRuleId: line.StatutoryRuleId);
	}
}

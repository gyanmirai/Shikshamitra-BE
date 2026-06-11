using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;

namespace ShikshaMitra.HR.Employees.Payroll;

public class EmployeePayrollContractWriter(
	IEmployeePayrollDetailsRepository payrollDetailsRepository,
	IEmployeePayrollComponentRepository payrollComponentRepository,
	IGuidGenerator guidGenerator) : ITransientDependency
{
	public async Task SaveAsync(
		Guid employeeId,
		PayrollContract input,
		IEnumerable<EmployeePayrollResolvedLine<EarningComponent>> earningLines,
		IEnumerable<EmployeePayrollResolvedLine<DeductionComponent>> deductionLines)
	{
		var amountPlan = EmployeePayrollComponentAmountPlanner.Create(earningLines, deductionLines);
		await UpsertPayrollDetailsAsync(employeeId, input);
		await ReplacePayrollComponentsAsync(employeeId, earningLines, deductionLines, amountPlan);
	}

	private async Task UpsertPayrollDetailsAsync(Guid employeeId, PayrollContract input)
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
				input.SalaryDetails?.EpfNumber,
				input.SalaryDetails?.UanNumber,
				input.SalaryDetails?.PunchId,
				input.SalaryDetails?.TotalWorkingHourPerMonth);
			await payrollDetailsRepository.InsertAsync(payrollDetails);
			return;
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
			salary != null ? salary.EpfNumber : payrollDetails.EpfNumber,
			salary != null ? salary.UanNumber : payrollDetails.UanNumber,
			salary != null ? salary.PunchId : payrollDetails.PunchId,
			salary != null ? salary.TotalWorkingHourPerMonth : payrollDetails.TotalWorkingHourPerMonth);
		await payrollDetailsRepository.UpdateAsync(payrollDetails);
	}

	private async Task ReplacePayrollComponentsAsync(
		Guid employeeId,
		IEnumerable<EmployeePayrollResolvedLine<EarningComponent>> earningLines,
		IEnumerable<EmployeePayrollResolvedLine<DeductionComponent>> deductionLines,
		EmployeePayrollComponentAmountPlan amountPlan)
	{
		await payrollComponentRepository.DeleteByEmployeeIdAsync(employeeId, isEarning: true);
		await payrollComponentRepository.DeleteByEmployeeIdAsync(employeeId, isEarning: false);

		foreach (var resolved in earningLines)
		{
			await payrollComponentRepository.InsertAsync(BuildEarningComponent(employeeId, resolved, amountPlan));
		}

		foreach (var resolved in deductionLines)
		{
			await payrollComponentRepository.InsertAsync(BuildDeductionComponent(employeeId, resolved, amountPlan));
		}
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
		var isFixed = master is not null
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
			master is null
				? resolved.ExistingPayrollComponent?.ComponentType ?? SalaryComponentType.Fixed
				: master.CalculationType == CalculationType.Amount
				? SalaryComponentType.Fixed
				: SalaryComponentType.Percentage,
			master is null ? resolved.ExistingPayrollComponent?.DependOn : PayrollComponentImpactProjection.MapDependency(master.DependOn),
			isEarning: true,
			usesMasterValue: EmployeePayrollComponentAmountPlanner.ShouldUseMasterValue(resolved, amount));
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
		var isFixed = master is not null
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
			master is null
				? resolved.ExistingPayrollComponent?.ComponentType ?? SalaryComponentType.Fixed
				: master.CalculationType == CalculationType.Amount
				? SalaryComponentType.Fixed
				: SalaryComponentType.Percentage,
			master is null ? resolved.ExistingPayrollComponent?.DependOn : PayrollComponentImpactProjection.MapDependency(master.DependOn),
			isEarning: false,
			usesMasterValue: EmployeePayrollComponentAmountPlanner.ShouldUseMasterValue(resolved, amount));
	}
}

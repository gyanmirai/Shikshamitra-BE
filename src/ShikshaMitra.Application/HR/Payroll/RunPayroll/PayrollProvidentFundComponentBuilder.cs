using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Templates;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollProvidentFundComponentBuilder(
	IProvidentFundRepository providentFundRepository,
	ILogger<PayrollProvidentFundComponentBuilder> logger)
	: ITransientDependency
{
	private Dictionary<Guid, ProvidentFund?>? readBatch;
	public IDisposable BeginReadBatch()
	{
		var previous = readBatch;
		readBatch = new();
		return new Volo.Abp.DisposeAction(() => readBatch = previous);
	}
	public async Task<List<EmployeePayrollComponent>> AddProvidentFundDeductionAsync(
		EmployeePayrollDetails? payrollDetails,
		List<EmployeePayrollComponent> components,
		int year,
		int month)
	{
		if (payrollDetails == null)
		{
			var result = components.ToList();
			result.RemoveAll(x => x.SemanticType == PayrollComponentSemanticType.EmployeeProvidentFund);
			return result;
		}

		return await AddProvidentFundDeductionAsync(
			payrollDetails.EmployeeId,
			Guid.TryParse(payrollDetails.PfRule, out var pfRuleId) ? pfRuleId : null,
			components,
			year,
			month);
	}

	public async Task<List<EmployeePayrollComponent>> AddProvidentFundDeductionAsync(
		Guid employeeId,
		Guid? pfRuleId,
		List<EmployeePayrollComponent> components,
		int year,
		int month)
	{
		var result = components.ToList();
		var rule = await ResolveRuleAsync(pfRuleId, employeeId, year, month);
		if (rule == null)
		{
			result.RemoveAll(x => x.SemanticType == PayrollComponentSemanticType.EmployeeProvidentFund);
			return result;
		}

		var baseAmount = Math.Max(0m, result.Where(x => PayrollProvidentFundBasis.IsEligible(x, rule.ContributionBasis)).Sum(x => x.Amount));
		if (rule.WageCeiling.HasValue)
		{
			baseAmount = Math.Min(baseAmount, rule.WageCeiling.Value);
		}

		var amount = Math.Round(baseAmount * (decimal)rule.EmployeePercentage / 100m, 2, MidpointRounding.AwayFromZero);
		var existing = result.FirstOrDefault(x => x.SemanticType == PayrollComponentSemanticType.EmployeeProvidentFund);
		if (existing != null)
		{
			existing.Amount = amount;
			existing.StatutoryRuleId = rule.Id;
			return result;
		}

		result.Add(new EmployeePayrollComponent(
			Guid.NewGuid(),
			employeeId,
			$"Provident Fund - {rule.Name}",
			componentId: null,
			description: $"{rule.EmployeePercentage:0.##}% staff PF contribution",
			amount,
			isFixed: true,
			isTemp: false,
			applicableMonth: new DateTime(year, month, 1),
			SalaryComponentType.Fixed,
			SalaryComponentDependency.None,
			isEarning: false,
			usesMasterValue: false,
			semanticType: PayrollComponentSemanticType.EmployeeProvidentFund,
			statutoryRuleId: rule.Id));

		return result;
	}

	public async Task<ProvidentFund?> ResolveRuleAsync(
		EmployeePayrollDetails? payrollDetails,
		int year,
		int month)
	{
		if (payrollDetails == null || !Guid.TryParse(payrollDetails.PfRule, out var pfRuleId))
		{
			return null;
		}

		return await ResolveRuleAsync(pfRuleId, payrollDetails.EmployeeId, year, month);
	}

	public async Task<ProvidentFund?> ResolveRuleAsync(
		Guid? pfRuleId,
		Guid employeeId,
		int year,
		int month)
	{
		if (!pfRuleId.HasValue)
		{
			return null;
		}

		ProvidentFund? rule;
		if (readBatch == null || !readBatch.TryGetValue(pfRuleId.Value, out rule))
		{
			rule = await providentFundRepository.FindAsync(pfRuleId.Value);
			if (readBatch != null) readBatch[pfRuleId.Value] = rule;
		}
		var effectiveDate = new DateTime(year, month, 1);
		if (rule is not { IsActive: true } ||
		    (rule.EmployeePercentage <= 0m && rule.EmployerPercentage <= 0m) ||
		    rule.EffectiveFrom > effectiveDate ||
		    rule.EffectiveTo.HasValue && rule.EffectiveTo.Value < effectiveDate)
		{
			logger.LogWarning(
				"Staff {EmployeeId} PF rule {PfRuleId} is unavailable or not effective for {EffectiveDate:yyyy-MM-dd}.",
				employeeId,
				pfRuleId,
				effectiveDate);
			return null;
		}

		return rule;
	}

}

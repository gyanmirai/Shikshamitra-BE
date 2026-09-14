using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Settings;
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

		var baseAmount = ResolveProvidentFundBaseAmount(result, rule.ContributionBasis);
		if (rule.WageCeiling.HasValue)
		{
			baseAmount = Math.Min(baseAmount, rule.WageCeiling.Value);
		}

		if (baseAmount <= 0m)
		{
			return result;
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
			description: $"{rule.EmployeePercentage:0.##}% employee PF contribution",
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
				"Employee {EmployeeId} PF rule {PfRuleId} is unavailable or not effective for {EffectiveDate:yyyy-MM-dd}.",
				employeeId,
				pfRuleId,
				effectiveDate);
			return null;
		}

		return rule;
	}

	private static decimal ResolveProvidentFundBaseAmount(
		IReadOnlyList<EmployeePayrollComponent> components,
		PfContributionBasis contributionBasis)
	{
		var semanticBase = components
			.Where(x => x.IsEarning &&
			            (x.SemanticType == PayrollComponentSemanticType.Basic ||
			             contributionBasis == PfContributionBasis.BasicAndDearnessAllowance &&
			             x.SemanticType == PayrollComponentSemanticType.DearnessAllowance))
			.Sum(x => x.Amount);
		if (semanticBase > 0m)
		{
			return semanticBase;
		}

		var basic = components
			.Where(x => x.IsEarning && IsBasicSalaryComponentName(x.Name))
			.Sum(x => x.Amount);

		return basic > 0m
			? basic
			: components.Where(x => x.IsEarning).Sum(x => x.Amount);
	}

	private static bool IsBasicSalaryComponentName(string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return false;
		}

		return string.Equals(name, "Basic", StringComparison.OrdinalIgnoreCase)
			|| name.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase);
	}
}

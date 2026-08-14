using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Payroll;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Templates;
using Volo.Abp;
using static ShikshaMitra.HR.Employees.Payroll.EmployeePayrollComponentAmountValidator;

namespace ShikshaMitra.HR.Employees.Payroll;

public static class EmployeePayrollComponentAmountPlanner
{
	public static EmployeePayrollComponentAmountPlan Create(
		IEnumerable<EmployeePayrollResolvedLine<EarningComponent>> earningLines,
		IEnumerable<EmployeePayrollResolvedLine<DeductionComponent>> deductionLines)
	{
		var resolvedEarnings = earningLines.ToList();
		var resolvedDeductions = deductionLines.ToList();

		EnsureNoComponentOverlap(resolvedEarnings, resolvedDeductions);
		var earningAmountLines = resolvedEarnings.Select(ToAmountLine).ToList();
		var deductionAmountLines = resolvedDeductions.Select(ToAmountLine).ToList();
		var earningAmountsByComponentId = PayrollComponentAmountResolver.ResolveEarningAmounts(earningAmountLines);
		var grossAmount = earningAmountsByComponentId.Values.Sum();
		var deductionAmountsByComponentId = PayrollComponentAmountResolver.ResolveDeductionAmounts(
			deductionAmountLines,
			earningAmountLines,
			earningAmountsByComponentId,
			grossAmount);

		ValidateEarningAmounts(resolvedEarnings, earningAmountsByComponentId);
		ValidateDeductionAmounts(resolvedDeductions, deductionAmountsByComponentId);

		return new EmployeePayrollComponentAmountPlan(earningAmountsByComponentId, deductionAmountsByComponentId);
	}

	public static decimal? ResolveEarningLineAmount(
		EmployeePayrollResolvedLine<EarningComponent> resolved,
		IReadOnlyDictionary<Guid, decimal> resolvedAmountsByComponentId)
	{
		if (resolved.Master?.Id is Guid masterId &&
		    resolvedAmountsByComponentId.TryGetValue(masterId, out var resolvedAmount))
		{
			return resolvedAmount;
		}

		return resolved.Line.Amount
			?? (resolved.Master is not null
				? ResolveEarningComponentAmount(resolved.Master)
				: resolved.ExistingPayrollComponent?.Amount);
	}

	public static decimal? ResolveDeductionLineAmount(
		EmployeePayrollResolvedLine<DeductionComponent> resolved,
		IReadOnlyDictionary<Guid, decimal> resolvedAmountsByComponentId)
	{
		if (resolved.Master?.Id is Guid masterId &&
		    resolvedAmountsByComponentId.TryGetValue(masterId, out var resolvedAmount))
		{
			return resolvedAmount;
		}

		return resolved.Line.Amount
			?? (resolved.Master is not null
				? ResolveDeductionComponentAmount(resolved.Master)
				: resolved.ExistingPayrollComponent?.Amount);
	}

	public static bool ShouldUseMasterValue(EmployeePayrollResolvedLine<EarningComponent> resolved, decimal amount)
	{
		if (resolved.Master is null || resolved.Line.IsTemp == true)
		{
			return false;
		}
		if (resolved.Line.UsesMasterValue == false)
		{
			return false;
		}

		if (resolved.Master.CalculationType == CalculationType.Percentage)
		{
			return true;
		}

		var masterAmount = ResolveEarningComponentAmount(resolved.Master);
		return amount == masterAmount;
	}

	public static bool ShouldUseMasterValue(EmployeePayrollResolvedLine<DeductionComponent> resolved, decimal amount)
	{
		if (resolved.Master is null || resolved.Line.IsTemp == true)
		{
			return false;
		}
		if (resolved.Line.UsesMasterValue == false)
		{
			return false;
		}

		if (resolved.Master.CalculationType == CalculationType.Percentage)
		{
			return true;
		}

		var masterAmount = ResolveDeductionComponentAmount(resolved.Master);
		return amount == masterAmount;
	}

	private static void ValidateEarningAmounts(
		IEnumerable<EmployeePayrollResolvedLine<EarningComponent>> earningLines,
		IReadOnlyDictionary<Guid, decimal> earningAmountsByComponentId)
	{
		foreach (var resolved in earningLines)
		{
			var amount = ResolveEarningLineAmount(resolved, earningAmountsByComponentId);
			if (!IsPercentageComponent(resolved) && (!amount.HasValue || amount <= 0))
			{
				throw new BusinessException(HRErrorCodes.EmployeePayrollComponentAmountInvalid)
					.WithData("componentId", (object?)resolved.Master?.Id ?? null!)
					.WithData("amount", (object?)amount ?? null!)
					.WithData("list", "earning");
			}
		}
	}

	private static void ValidateDeductionAmounts(
		IEnumerable<EmployeePayrollResolvedLine<DeductionComponent>> deductionLines,
		IReadOnlyDictionary<Guid, decimal> deductionAmountsByComponentId)
	{
		foreach (var resolved in deductionLines)
		{
			var amount = ResolveDeductionLineAmount(resolved, deductionAmountsByComponentId);
			var isZeroValueStatutoryLine = amount == 0m &&
				(resolved.Line.SemanticType is PayrollComponentSemanticType.EmployeeProvidentFund or
					PayrollComponentSemanticType.ProfessionalTax);
			if (!IsPercentageComponent(resolved) && (!amount.HasValue || amount < 0m || amount == 0m && !isZeroValueStatutoryLine))
			{
				throw new BusinessException(HRErrorCodes.EmployeePayrollComponentAmountInvalid)
					.WithData("componentId", (object?)resolved.Master?.Id ?? null!)
					.WithData("amount", (object?)amount ?? null!)
					.WithData("list", "deduction");
			}
		}
	}

	private static decimal ResolveEarningComponentAmount(EarningComponent master) =>
		master.CalculationType == CalculationType.Amount
			? master.Amount ?? 0m
			: master.Percentage ?? 0m;

	private static decimal ResolveDeductionComponentAmount(DeductionComponent master) =>
		master.CalculationType == CalculationType.Amount
			? master.Amount ?? 0m
			: master.Percentage ?? 0m;

	private static bool IsPercentageComponent(EmployeePayrollResolvedLine<EarningComponent> resolved)
	{
		return resolved.Master?.CalculationType == CalculationType.Percentage && resolved.Line.UsesMasterValue != false;
	}

	private static bool IsPercentageComponent(EmployeePayrollResolvedLine<DeductionComponent> resolved)
	{
		return resolved.Master?.CalculationType == CalculationType.Percentage && resolved.Line.UsesMasterValue != false;
	}

	private static PayrollComponentAmountLine<EarningComponent> ToAmountLine(
		EmployeePayrollResolvedLine<EarningComponent> resolved)
	{
		var line = resolved.Line;
		return new PayrollComponentAmountLine<EarningComponent>(
			line.Id,
			line.ComponentId,
			resolved.Master?.Name ?? line.Name,
			line.Amount,
			line.IsTemp ?? resolved.ExistingPayrollComponent?.IsTemp ?? resolved.Master is null,
			line.UsesMasterValue,
			resolved.Master);
	}

	private static PayrollComponentAmountLine<DeductionComponent> ToAmountLine(
		EmployeePayrollResolvedLine<DeductionComponent> resolved)
	{
		var line = resolved.Line;
		return new PayrollComponentAmountLine<DeductionComponent>(
			line.Id,
			line.ComponentId,
			resolved.Master?.Name ?? line.Name,
			line.Amount,
			line.IsTemp ?? resolved.ExistingPayrollComponent?.IsTemp ?? resolved.Master is null,
			line.UsesMasterValue,
			resolved.Master);
	}
}

public sealed record EmployeePayrollComponentAmountPlan(
	IReadOnlyDictionary<Guid, decimal> EarningAmountsByComponentId,
	IReadOnlyDictionary<Guid, decimal> DeductionAmountsByComponentId);

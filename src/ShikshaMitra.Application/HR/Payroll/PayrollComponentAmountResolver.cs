using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll;

internal sealed record PayrollComponentAmountLine<TMaster>(
	Guid? Id,
	Guid? ComponentId,
	string? Name,
	decimal? Amount,
	bool IsTemp,
	TMaster? Master)
	where TMaster : class;

internal static class PayrollComponentAmountResolver
{
	public static Dictionary<Guid, decimal> ResolveEarningAmounts(
		IReadOnlyList<PayrollComponentAmountLine<EarningComponent>> earnings)
	{
		var amountsByComponentId = new Dictionary<Guid, decimal>();

		foreach (var earning in earnings.Where(x => x.Master?.CalculationType != CalculationType.Percentage))
		{
			if (earning.Master?.Id is Guid masterId)
			{
				amountsByComponentId[masterId] = ResolveAmountComponentFallback(earning.Amount, earning.Master.Amount);
			}
		}

		var pendingPercentageEarnings = earnings
			.Where(x => x.Master?.CalculationType == CalculationType.Percentage)
			.ToList();
		while (pendingPercentageEarnings.Count > 0)
		{
			var resolvedAny = false;
			foreach (var earning in pendingPercentageEarnings.ToList())
			{
				var master = earning.Master!;
				if (!TryResolvePercentageBaseAmount(
					    master.DependOn,
					    master.SelectedEarningComponentId,
					    earnings,
					    amountsByComponentId,
					    new Dictionary<Guid, decimal>(),
					    out var baseAmount))
				{
					continue;
				}

				amountsByComponentId[master.Id] = CalculatePercentageAmount(baseAmount, master.Percentage);
				pendingPercentageEarnings.Remove(earning);
				resolvedAny = true;
			}

			if (!resolvedAny)
			{
				var unresolved = pendingPercentageEarnings[0].Master!;
				throw new BusinessException(HRErrorCodes.EmployeePayrollInvalidEarningComponent)
					.WithData("componentId", unresolved.SelectedEarningComponentId?.ToString() ?? string.Empty)
					.WithData("dependency", unresolved.DependOn.ToString());
			}
		}

		return amountsByComponentId;
	}

	public static Dictionary<Guid, decimal> ResolveDeductionAmounts(
		IReadOnlyList<PayrollComponentAmountLine<DeductionComponent>> deductions,
		IReadOnlyList<PayrollComponentAmountLine<EarningComponent>> earnings,
		IReadOnlyDictionary<Guid, decimal> earningAmountsByComponentId,
		decimal grossAmount)
	{
		var amountsByComponentId = new Dictionary<Guid, decimal>();

		foreach (var deduction in deductions.Where(x => x.Master?.CalculationType != CalculationType.Percentage))
		{
			if (deduction.Master?.Id is Guid masterId)
			{
				amountsByComponentId[masterId] = ResolveAmountComponentFallback(deduction.Amount, deduction.Master.Amount);
			}
		}

		var pendingPercentageDeductions = deductions
			.Where(x => x.Master?.CalculationType == CalculationType.Percentage)
			.ToList();
		while (pendingPercentageDeductions.Count > 0)
		{
			var resolvedAny = false;
			foreach (var deduction in pendingPercentageDeductions.ToList())
			{
				var master = deduction.Master!;
				if (!TryResolvePercentageBaseAmount(
					    master.DependOn,
					    master.SelectedDeductionComponentId,
					    earnings,
					    earningAmountsByComponentId,
					    amountsByComponentId,
					    out var baseAmount,
					    grossAmount))
				{
					continue;
				}

				amountsByComponentId[master.Id] = CalculatePercentageAmount(baseAmount, master.Percentage);
				pendingPercentageDeductions.Remove(deduction);
				resolvedAny = true;
			}

			if (!resolvedAny)
			{
				var unresolved = pendingPercentageDeductions[0].Master!;
				throw new BusinessException(HRErrorCodes.EmployeePayrollInvalidDeductionComponent)
					.WithData("componentId", unresolved.SelectedDeductionComponentId?.ToString() ?? string.Empty)
					.WithData("dependency", unresolved.DependOn.ToString());
			}
		}

		return amountsByComponentId;
	}

	public static decimal ResolveAmountComponentFallback(decimal? lineAmount, decimal? masterAmount) =>
		lineAmount ?? masterAmount ?? 0m;

	public static decimal CalculatePercentageAmount(decimal baseAmount, decimal? percentage) =>
		Math.Round(baseAmount * (percentage ?? 0m) / 100m, 2, MidpointRounding.AwayFromZero);

	private static bool TryResolvePercentageBaseAmount(
		ComponentDependency dependOn,
		Guid? selectedComponentId,
		IReadOnlyList<PayrollComponentAmountLine<EarningComponent>>? earnings,
		IReadOnlyDictionary<Guid, decimal> earningAmountsByComponentId,
		IReadOnlyDictionary<Guid, decimal> deductionAmountsByComponentId,
		out decimal amount,
		decimal? grossAmount = null)
	{
		switch (dependOn)
		{
			case ComponentDependency.Basic:
				return TryResolveBasicAmount(earnings, earningAmountsByComponentId, out amount);
			case ComponentDependency.Gross:
				amount = grossAmount ?? earningAmountsByComponentId.Values.Sum();
				return true;
			case ComponentDependency.CustomComponent when selectedComponentId.HasValue &&
				earningAmountsByComponentId.TryGetValue(selectedComponentId.Value, out var earningAmount):
				amount = earningAmount;
				return true;
			case ComponentDependency.CustomComponent when selectedComponentId.HasValue &&
				deductionAmountsByComponentId.TryGetValue(selectedComponentId.Value, out var deductionAmount):
				amount = deductionAmount;
				return true;
			default:
				amount = 0m;
				return false;
		}
	}

	private static bool TryResolveBasicAmount(
		IReadOnlyList<PayrollComponentAmountLine<EarningComponent>>? earnings,
		IReadOnlyDictionary<Guid, decimal> earningAmountsByComponentId,
		out decimal amount)
	{
		if (earnings != null)
		{
			var basic = earnings.FirstOrDefault(x =>
				x.Master?.Id is Guid masterId &&
				earningAmountsByComponentId.ContainsKey(masterId) &&
				IsBasicSalaryComponentName(x.Name));
			if (basic?.Master?.Id is Guid masterId)
			{
				amount = earningAmountsByComponentId[masterId];
				return true;
			}
		}

		if (earningAmountsByComponentId.Count == 1)
		{
			amount = earningAmountsByComponentId.Values.Single();
			return true;
		}

		amount = 0m;
		return false;
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

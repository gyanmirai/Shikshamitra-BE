using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

/// <summary>Frozen source-period differences, applied after the target's ordinary statutory calculation.</summary>
internal static class PayslipSettlementProjection
{
	internal sealed record Difference(string Kind, string Name, decimal Amount);

	internal static List<Difference> Differences(PayslipCorrection correction)
	{
		using var original = JsonDocument.Parse(correction.OriginalSnapshotJson);
		var candidate = JsonSerializer.Deserialize<RunPayrollItem>(correction.CandidateJson!)
			?? throw new BusinessException(HRErrorCodes.PayslipConflict);
		var differences = new List<Difference>();
		Add("EARNING", "EarningComponentsJson", candidate.EarningComponents);
		Add("DEDUCTION", "DeductionComponentsJson", candidate.DeductionComponents);
		Add("EMPLOYER", "EmployerContributionsJson", candidate.EmployerContributions);
		var net = Money(candidate.NetPay) - original.RootElement.GetProperty("NetPay").GetDecimal();
		if (differences.Where(x => x.Kind == "EARNING").Sum(x => x.Amount) -
			differences.Where(x => x.Kind == "DEDUCTION").Sum(x => x.Amount) != net)
			throw new BusinessException(HRErrorCodes.PayslipSettlementInvalid).WithData("reason", "Source component differences do not reconcile with source net pay.");
		return differences;

		void Add(string kind, string field, List<RunPayrollComponentItem> revised)
		{
			var previous = JsonSerializer.Deserialize<List<RunPayrollComponentItem>>(original.RootElement.GetProperty(field).GetString()!)!;
			var old = previous.GroupBy(x => x.Name).ToDictionary(x => x.Key, x => x.Sum(y => Money(y.Amount)));
			var next = revised.GroupBy(x => x.Name).ToDictionary(x => x.Key, x => x.Sum(y => Money(y.Amount)));
			foreach (var name in old.Keys.Union(next.Keys).OrderBy(x => x, StringComparer.Ordinal))
			{
				var difference = next.GetValueOrDefault(name) - old.GetValueOrDefault(name);
				if (difference != 0) differences.Add(new(kind, name, difference));
			}
		}
	}

	internal static void Apply(RunPayrollItem target, PayslipCorrection correction)
	{
		var differences = Differences(correction);
		var sourceEarnings = differences.Where(x => x.Kind == "EARNING").Sum(x => x.Amount);
		var sourceDeductions = differences.Where(x => x.Kind == "DEDUCTION").Sum(x => x.Amount);
		// Social Security Code s31: never collect source-period employee insurance from unrelated target wages.
		// Additional source-period deductions must be funded by the source-period arrears being paid here.
		if (differences.Where(x => x.Kind == "DEDUCTION" && x.Amount > 0).Sum(x => x.Amount) > Math.Max(0, sourceEarnings))
			throw new BusinessException(HRErrorCodes.PayslipSettlementInvalid).WithData("reason", "Source-period deductions exceed the corresponding arrears. Resolve statutory recovery before settlement.");
		var recovery = differences.Where(x => x.Kind == "EARNING" && x.Amount < 0).Sum(x => -x.Amount);
		var newDeductions = differences.Where(x => x.Kind == "DEDUCTION" && x.Amount > 0).Sum(x => x.Amount) + recovery;
		// Only known wage components support automatic recovery. Never guess a statutory wage base from CTC.
		if (recovery > 0)
		{
			var wages = StatutoryRecoveryWages(target.EarningComponents);
			if (Money(target.TotalDeduction) + newDeductions > wages / 2m)
				throw new BusinessException(HRErrorCodes.PayslipSettlementInvalid).WithData("reason", "This settlement exceeds the 50% wage-period deduction limit. Select a payroll with sufficient recoverable wages.");
		}
		var beforeNet = Money(target.NetPay);
		var beforeCost = Money(target.EmployerCost);
		foreach (var delta in differences)
		{
			var label = $"{delta.Name} ({correction.SourceMonth} correction)";
			var line = new RunPayrollComponentItem { Name = label, Amount = (double)Math.Abs(delta.Amount), IsTemp = true,
				Description = $"Source document {correction.SourceDocumentId}; correction {correction.Id}; {delta.Kind} difference {delta.Amount:0.00}" };
			if (delta.Kind == "EMPLOYER")
			{
				line.Amount = (double)delta.Amount;
				target.EmployerContributions.Add(line);
			}
			else if ((delta.Kind == "EARNING" && delta.Amount > 0) || (delta.Kind == "DEDUCTION" && delta.Amount < 0))
				target.EarningComponents.Add(line);
			else target.DeductionComponents.Add(line);
		}
		target.TotalDeduction = (double)target.DeductionComponents.Sum(x => Money(x.Amount));
		target.NetPay = (double)(target.EarningComponents.Sum(x => Money(x.Amount)) - Money(target.TotalDeduction));
		target.EmployerContributionTotal = (double)target.EmployerContributions.Sum(x => Money(x.Amount));
		// Recoveries/refunds are cash settlements, not new earned wages or new employer costs.
		target.EmployerCost = (double)(beforeCost + sourceEarnings + differences.Where(x => x.Kind == "EMPLOYER").Sum(x => x.Amount));
		if (Money(target.NetPay) != beforeNet + sourceEarnings - sourceDeductions || target.NetPay < 0)
			throw new BusinessException(HRErrorCodes.PayslipSettlementInvalid).WithData("reason", "Settlement cannot reconcile within the selected payroll.");
		target.NetPayDelta = target.PreviousMonthNetPay.HasValue ? target.NetPay - target.PreviousMonthNetPay.Value : null;
		target.NetPayDeltaPercent = target.PreviousMonthNetPay is not (null or 0)
			? target.NetPayDelta / target.PreviousMonthNetPay.Value * 100 : null;
	}

	private static decimal StatutoryRecoveryWages(List<RunPayrollComponentItem> components)
	{
		decimal included = 0, excluded = 0;
		// Cash correction refunds/arrears do not enlarge the target's regular wage-period recovery limit.
		foreach (var component in components.Where(x => !x.IsTemp && x.Amount != 0))
		{
			switch (component.Name.Trim().ToUpperInvariant())
			{
				case "BASIC": case "BASIC SALARY": case "DEARNESS ALLOWANCE": case "DEARNESS ALLOWANCE (DA)":
				case "DA": case "RETAINING ALLOWANCE": case "SPECIAL ALLOWANCE": case "MEDICAL ALLOWANCE":
					included += Money(component.Amount); break;
				case "HOUSE RENT ALLOWANCE (HRA)": case "HOUSE RENT ALLOWANCE": case "HRA":
				case "TRANSPORT ALLOWANCE": case "CONVEYANCE ALLOWANCE":
					excluded += Money(component.Amount); break;
				default:
					throw new BusinessException(HRErrorCodes.PayslipSettlementInvalid).WithData("reason", "The selected payroll has earnings without a verified wage-recovery classification. Resolve wage classifications before recovery.");
			}
		}
		// Code on Wages s2(y): the listed exclusions cannot reduce wages below half these regular payments.
		return Math.Max(included, (included + excluded) / 2m);
	}
	private static decimal Money(double amount) => Math.Round((decimal)amount, 2, MidpointRounding.AwayFromZero);
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.FeeCollection;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class LateFeeBalanceService
{
	private const string CommonExplanation = "Common late fee across selected fee heads and overdue installments. ";

	private async Task AccrueContextAsync(AccrualContext context, Guid academicYearId, DateTime asOf,
		bool preserveAssessment, Dictionary<Guid, decimal> paid, Dictionary<Guid, decimal> waived,
		Dictionary<Guid, decimal> reconciliation, Dictionary<Guid, List<FeeCollectionDueFeeAllocation>> historical,
		Dictionary<Guid, FeeRecordMetadata> transactions, List<CollectionPolicyExceptionWindow> windowEvidence,
		Dictionary<(Guid StructureId, DateTime Date), ActiveExceptionWindow?> windows,
		Dictionary<Guid, Balance> result, List<DueFee> changed)
	{
		foreach (var due in context.Dues)
		{
			var explanation = due.GetLateFeeAssessmentExplanation() ?? string.Empty;
			result[due.Id] = new(Math.Max(due.GetAssessedLateFeeAmount(), paid.GetValueOrDefault(due.Id) + waived.GetValueOrDefault(due.Id)),
				paid.GetValueOrDefault(due.Id), waived.GetValueOrDefault(due.Id), explanation,
				reconciliation.GetValueOrDefault(due.Id), explanation.StartsWith(CommonExplanation, StringComparison.Ordinal));
		}
		var rules = context.Rules;
		if (rules.Policy is not { EnableLateFee: true } policy || rules.IsLateFeeExempt) return;
		// The selected heads are one applicability scope. Only explicit CustomRule overrides
		// form a separate rule group; selecting another inherited head never multiplies a fee.
		var groups = context.Dues.Where(d => policy.AppliesLateFeeTo(d.FeeHeadId) &&
			(!policy.TryGetFeeHeadOverride(d.FeeHeadId, out var rule) || rule?.OverrideMode != FeeHeadOverrideMode.NoLateFee))
			.GroupBy(d => policy.TryGetFeeHeadOverride(d.FeeHeadId, out var rule) && rule?.OverrideMode == FeeHeadOverrideMode.CustomRule
				? d.FeeHeadId : Guid.Empty);
		foreach (var group in groups)
		{
			var rows = group.Where(d => context.StartBoundary?.Allows(d) != false).OrderBy(d => d.DueDate).ThenBy(d => d.Id).ToList();
			if (rows.Count == 0) continue;
			var anchor = rows.OrderByDescending(d => result[d.Id].Total > 0m).ThenBy(d => d.DueDate).ThenBy(d => d.Id).First();
			// Retain historical assessments on excluded rows without issuing the same
			// common charge again against a newly eligible installment.
			var preservedTotal = group.Sum(d => result[d.Id].Total);
			var target = preservedTotal;
			var explanation = result[anchor.Id].Explanation;
			bool CanRecover(DateTime timestamp, DateTime calendarDate) => context.CanRecover(timestamp) && !windowEvidence.Any(x => x.CoversDate(calendarDate)
				&& (x.Scope != ExceptionWindowScope.FeeStructure || x.ScopeId == context.StructureId)
				&& (x.CreationTime > timestamp || x.LastModificationTime > timestamp || x.DeletionTime > timestamp));
			async Task<ActiveExceptionWindow?> WindowAsync(DateTime date)
			{
				var key = (context.StructureId, date.Date);
				if (!windows.TryGetValue(key, out var window))
					windows[key] = window = await windowResolver.ResolveActiveWindowAsync(academicYearId, key.Item1, key.Item2);
				return window;
			}
			void Accept(LateFeeCalculationResult calculated)
			{
				if (calculated.LateFee <= target) return;
				target = calculated.LateFee;
				explanation = CommonExplanation + calculated.Explanation;
			}
			// Legacy recovery uses only immutable pre-payment principal/date evidence. It
			// must never manufacture today's overdue days for a fully settled obligation.
			foreach (var due in rows)
				foreach (var payment in historical.GetValueOrDefault(due.Id, []))
					if (TryReadPrincipalBefore(transactions[payment.FeeRecordMetadataId].CommandResultJson, due.Id, out var before, out var timestamp, out var date)
						&& CanRecover(timestamp, date) && due.IsBillGeneratedOn(date))
					{
						var window = await WindowAsync(date);
						Accept(calculator.Calculate(policy, new((date.Date - due.DueDate.Date).Days, before, due.OriginalAmount, before,
							due.FeeHeadId, false, rules.CustomGracePeriodDays, window?.Effect, window?.ExtensionDays, window?.Label)));
					}
			var active = rows.Where(d => d.OutstandingAmount > 0m && d.DueDate.Date < asOf.Date && d.IsBillGeneratedOn(asOf)).ToList();
			var type = policy.TryGetFeeHeadOverride(rows[0].FeeHeadId, out var custom) && custom?.OverrideMode == FeeHeadOverrideMode.CustomRule
				? custom.LateFeeRule?.LateFeeType ?? policy.LateFeeType : policy.LateFeeType;
			var closedIntervals = new List<(DateTime Start, DateTime End)>();
			foreach (var due in rows.Where(d => d.OutstandingAmount <= 0m))
			{
				DateTime? settled = null;
				foreach (var payment in historical.GetValueOrDefault(due.Id, []))
					if (TryReadPrincipalBefore(transactions[payment.FeeRecordMetadataId].CommandResultJson, due.Id, out var before, out var timestamp, out var date)
						&& before > 0m && before <= (decimal)payment.Amount && date.Date <= asOf.Date && CanRecover(timestamp, date))
						settled = !settled.HasValue || date.Date < settled.Value ? date.Date : settled;
				if (!settled.HasValue || !due.IsBillGeneratedOn(settled.Value)) continue;
				var historicalWindow = await WindowAsync(settled.Value);
				if (historicalWindow?.Effect == ExceptionWindowEffect.SuspendLateFeeAccrual) continue;
				var historicalGrace = (rules.CustomGracePeriodDays ?? policy.GracePeriodDays) +
					(historicalWindow?.Effect == ExceptionWindowEffect.ExtendGracePeriod ? historicalWindow.ExtensionDays ?? 0 : 0);
				var start = due.DueDate.Date.AddDays(historicalGrace);
				if (settled.Value > start) closedIntervals.Add((start, settled.Value));
			}
			// Historical fixed-rate recovery has its own windows. Today's prospective
			// suspension/extension cannot erase late days already evidenced at settlement.
			if (type is LateFeeCalculationType.Flat or LateFeeCalculationType.PerDay or LateFeeCalculationType.PerWeek or LateFeeCalculationType.PerMonth)
				Accept(calculator.Calculate(policy, new(CountOverdueDays(closedIntervals), 0m, 0m, 0m,
					rows[0].FeeHeadId, CustomGracePeriodDays: 0)));
			if (active.Count > 0)
			{
				var window = await WindowAsync(asOf);
				var grace = (rules.CustomGracePeriodDays ?? policy.GracePeriodDays) +
					(window?.Effect == ExceptionWindowEffect.ExtendGracePeriod ? window.ExtensionDays ?? 0 : 0);
				var intervals = new List<(DateTime Start, DateTime End)>(closedIntervals);
				foreach (var due in active)
				{
					var start = due.DueDate.Date.AddDays(grace);
					if (asOf.Date > start) intervals.Add((start, asOf.Date));
				}
				var days = CountOverdueDays(intervals);
				var outstanding = active.Sum(d => d.OutstandingAmount);
				Accept(calculator.Calculate(policy, new(days + grace, outstanding, active.Sum(d => d.OriginalAmount), outstanding,
					rows[0].FeeHeadId, false, rules.CustomGracePeriodDays, window?.Effect, window?.ExtensionDays, window?.Label)));
			}
			// Keep historical amounts on their original rows and allocate only common growth.
			var delta = target - preservedTotal;
			if (delta > 0m) result[anchor.Id] = result[anchor.Id] with
			{
				Total = result[anchor.Id].Total + delta, Explanation = explanation, IsCommonCharge = true,
			};
			foreach (var due in rows.Where(d => preserveAssessment && result[d.Id].Total > d.GetAssessedLateFeeAmount()))
			{
				due.PreserveLateFeeAssessment(result[due.Id].Total, new(DateTime.SpecifyKind(asOf, DateTimeKind.Utc)),
					context.VersionId, result[due.Id].Explanation);
				changed.Add(due);
			}
		}
	}

	private static int CountOverdueDays(List<(DateTime Start, DateTime End)> intervals)
	{
		var days = 0;
		DateTime? through = null;
		foreach (var interval in intervals.OrderBy(x => x.Start))
		{
			var start = through.HasValue && through.Value > interval.Start ? through.Value : interval.Start;
			if (interval.End > start) days += (interval.End - start).Days;
			if (!through.HasValue || interval.End > through.Value) through = interval.End;
		}
		return days;
	}
}

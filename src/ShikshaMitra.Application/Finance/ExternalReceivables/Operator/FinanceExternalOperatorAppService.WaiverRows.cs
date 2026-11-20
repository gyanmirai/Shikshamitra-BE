using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.ExternalReceivables.Operator;

public partial class FinanceExternalOperatorAppService
{
	private async Task<List<FinanceExternalWaiverContextItemDto>> BuildWaiverContextRowsAsync(
		FinanceExternalSettlementContext context, FinanceExternalWaiverAction action,
		FinanceExternalSettlementPolicy? binding, List<WaiverContextRequest> requests, FinanceExternalWaiverContextDto result)
	{
		if (requests.Count == 0) return [];
		var ledger = await LazyServiceProvider.LazyGetRequiredService<FinanceExternalChargeBalanceReader>().ReadAsync(context.Charge);
		var applications = ledger.Waivers.ToDictionary(row => row.Id);
		var reversals = ledger.Reversals.GroupBy(row => row.WaiverApplicationId)
			.ToDictionary(group => group.Key, group => group.Sum(row => row.Amount));
		return requests.Select(request =>
		{
			var application = request.ApplicationId.HasValue ? applications.GetValueOrDefault(request.ApplicationId.Value) : null;
			var reversed = request.ApplicationId.HasValue ? reversals.GetValueOrDefault(request.ApplicationId.Value) : 0m;
			var row = new FinanceExternalWaiverContextItemDto
			{
				RequestId = request.Id, ApplicationId = request.ApplicationId, State = request.State,
				RequestedAmount = request.Amount, AppliedAmount = application?.EffectiveAmount ?? 0m,
				ReversedAmount = reversed, ReversibleAmount = application == null ? 0m : application.EffectiveAmount - reversed,
				Reason = request.Reason, Issues = [.. result.Issues]
			};
			if (binding == null || request.PolicyVersionId != binding.CollectionPolicyVersionId)
				row.Issues.Add("The request's recorded policy binding requires Finance review.");
			if (action is FinanceExternalWaiverAction.Approve or FinanceExternalWaiverAction.Reject)
			{
				if (request.MakerId == context.ActorId) row.Issues.Add("A different authorized person must perform the independent review.");
				if (!request.RequiresApproval || request.State != FinanceExternalWaiverState.PendingApproval)
					row.Issues.Add("Only a pending request can receive this review decision.");
				if (action == FinanceExternalWaiverAction.Approve && request.Amount > ledger.Amounts.Collectible)
					row.Issues.Add("The requested waiver exceeds the current collectible balance.");
			}
			else if (action == FinanceExternalWaiverAction.Reverse &&
				(request.State != FinanceExternalWaiverState.Applied || application == null || row.ReversibleAmount <= 0m))
				row.Issues.Add("There is no evidenced applied waiver amount available to reverse.");
			row.CanAct = action != FinanceExternalWaiverAction.Apply && row.Issues.Count == 0;
			return row;
		}).ToList();
	}
}

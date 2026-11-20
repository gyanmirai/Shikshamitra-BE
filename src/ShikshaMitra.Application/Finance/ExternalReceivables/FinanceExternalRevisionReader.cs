using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.ExternalReceivables;

public class FinanceExternalRevisionReader(FinanceExternalChargeBalanceReader balances,
	IRepository<FinanceExternalWaiverRequest, Guid> waiverRequests, ICurrentTenant tenant) : ITransientDependency
{
	public async Task<string> ReadAsync(FinanceExternalSettlementContext context, Guid? bindingId)
	{
		var ledger = await balances.ReadAsync(context.Charge);
		var facts = ledger.Allocations.Select(row => new FinanceExternalLedgerFact(row.Id, "Payment",
			ledger.Collections.Single(header => header.Id == row.FeeRecordMetadataId).Status.ToString(), row.Amount, row.FeeRecordMetadataId))
			.Concat(ledger.Waivers.Select(row => new FinanceExternalLedgerFact(row.Id, "Waiver", "Applied", row.EffectiveAmount)))
			.Concat(ledger.Reversals.Select(row => new FinanceExternalLedgerFact(row.Id, "WaiverReversal", "Applied", row.Amount, row.WaiverApplicationId)))
			.ToList();
		facts.AddRange((await waiverRequests.GetListAsync(row => row.TenantId == tenant.Id && row.SourceLinkId == context.Link.Id))
			.Select(row => new FinanceExternalLedgerFact(row.Id, "WaiverRequest", row.State.ToString(), row.Amount, row.AppliedWaiverId)));
		return FinanceExternalBalanceRevision.Create(context, bindingId, facts);
	}
}

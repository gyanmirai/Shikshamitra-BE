using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Library;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.ExternalReceivables;

[ExposeServices(typeof(IFinanceExternalRefundEvidenceQuery))]
public partial class EfCoreFinanceExternalRefundEvidenceQuery(IDbContextProvider<ShikshaMitraDbContext> contexts,
	ICurrentTenant tenant) : IFinanceExternalRefundEvidenceQuery, ITransientDependency
{
	public async Task<IQueryable<FinanceExternalRefundChargeTotal>> GetQueryAsync(IQueryable<Guid> relevantChargeIds,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var school = tenant.Id ?? throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource);
		var db = await contexts.GetDbContextAsync();
		var facts = ReadFacts(db, school, relevantChargeIds);
		Expression<Func<FinanceExternalRefundEvidenceInput, RefundLineOutcome>> outcome = value => new()
		{
			OwnerChargeId = value.OwnerChargeId, ClaimedChargeId = value.ClaimedChargeId,
			Amount = FinanceExternalRefundEvidenceExpressions.IsValid(value) ? value.Processed - value.Reversed : 0m,
			Invalid = FinanceExternalRefundEvidenceExpressions.IsValid(value) ? 0 : 1
		};
		var lines = facts.Select(FinanceExternalPositionExpressionExpansion.Expand(outcome));
		// Valid money belongs to its verified owner once. An invalid line also
		// implicates a distinct claimed charge even when it no longer names its owner.
		var owners = lines.Where(row => row.OwnerChargeId.HasValue).Select(row => new FinanceExternalRefundChargeTotal
			{ ChargeId = row.OwnerChargeId!.Value, Amount = row.Amount, InvalidCount = row.Invalid });
		var claims = lines.Where(row => row.Invalid > 0 && row.ClaimedChargeId.HasValue && row.ClaimedChargeId != row.OwnerChargeId)
			.Select(row => new FinanceExternalRefundChargeTotal
				{ ChargeId = row.ClaimedChargeId!.Value, Amount = 0m, InvalidCount = 1 });
		return owners.Concat(claims).Where(row => relevantChargeIds.Contains(row.ChargeId)).GroupBy(row => row.ChargeId)
			.Select(group => new FinanceExternalRefundChargeTotal
				{ ChargeId = group.Key, Amount = group.Sum(row => row.Amount), InvalidCount = group.Sum(row => row.InvalidCount) });
	}

	private sealed class RefundLineOutcome
	{
		public Guid? OwnerChargeId { get; init; }
		public Guid? ClaimedChargeId { get; init; }
		public decimal Amount { get; init; }
		public int Invalid { get; init; }
	}
}

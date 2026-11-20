using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Library;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.ExternalReceivables;

[ExposeServices(typeof(IFinanceExternalPositionQuery))]
public partial class EfCoreFinanceExternalPositionQuery(IDbContextProvider<ShikshaMitraDbContext> provider, ICurrentTenant tenant,
	IFinanceExternalRefundEvidenceQuery refundEvidence)
	: IFinanceExternalPositionQuery, ITransientDependency
{
	public async Task<IQueryable<FinanceExternalPositionQueryRow>> GetQueryAsync(
		IQueryable<FinanceExternalPositionSource> authorizedSources,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (!tenant.Id.HasValue) throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource);
		var tenantId = tenant.Id.Value;
		var db = await provider.GetDbContextAsync();
		var postgres = db.Database.ProviderName switch
		{
			"Npgsql.EntityFrameworkCore.PostgreSQL" => true,
			"Microsoft.EntityFrameworkCore.Sqlite" => false,
			_ => throw new NotSupportedException("Canonical Finance positions require PostgreSQL or the test SQLite provider.")
		};
		var sources = authorizedSources.Where(source => source.TenantId == tenantId);
		var accepted = db.Database.SqlQueryRaw<FinanceExternalAcceptedEvidence>(
			FinanceExternalAcceptedEvidenceSql.Build(postgres), tenantId, FinanceExternalEvidenceJson.WhiteSpaceCharacters);
		var relevant = from source in sources
			join evidence in accepted on new { Id = (Guid?)source.SourceId, Kind = (FinanceExternalSourceKind?)source.Kind }
				equals new { Id = evidence.SourceId, Kind = evidence.SourceKind }
			where evidence.FinanceChargeId.HasValue
			select evidence.FinanceChargeId.Value;
		var payments = PaymentTotals(db, tenantId, relevant);
		var waivers = WaiverTotals(db, tenantId, relevant);
		var refunds = await refundEvidence.GetQueryAsync(relevant, cancellationToken);
		// The unique tenant/kind/source key permits one candidate. Retain the
		// actual outer reason in the bounded lookup so evidence follows key matching.
		var pairs = postgres
			? from source in sources
			  from candidate in accepted.Where(row => row.SourceId == source.SourceId && row.SourceKind == source.Kind)
				  .Select(evidence => new { evidence, currentReason = source.CurrentLegacyResolvedReason })
				  .Take(1).DefaultIfEmpty()
			  select new { source, candidate.evidence, currentReason = candidate.currentReason ?? source.CurrentLegacyResolvedReason }
			: from source in sources
			  join evidence in accepted on new { Id = (Guid?)source.SourceId, Kind = (FinanceExternalSourceKind?)source.Kind }
				  equals new { Id = evidence.SourceId, Kind = evidence.SourceKind } into acceptedJoin
			  from evidence in acceptedJoin.DefaultIfEmpty()
			  select new { source, evidence, currentReason = source.CurrentLegacyResolvedReason };
		var data = from pair in pairs
			let source = pair.source
			let evidence = pair.evidence
			join payment in payments on evidence.FinanceChargeId equals (Guid?)payment.ChargeId into paymentJoin
			from payment in paymentJoin.DefaultIfEmpty()
			join waiver in waivers on evidence.FinanceChargeId equals (Guid?)waiver.ChargeId into waiverJoin
			from waiver in waiverJoin.DefaultIfEmpty()
			join refund in refunds on evidence.FinanceChargeId equals (Guid?)refund.ChargeId into refundJoin
			from refund in refundJoin.DefaultIfEmpty()
			select new FinanceExternalPositionAggregate
			{
				Source = source, Accepted = evidence, CurrentReason = pair.currentReason,
				Balance = new()
				{
					Original = evidence.Amount ?? 0m, Status = evidence.Status ?? ChargeStatus.Pending,
					Accepted = (decimal?)payment.Accepted ?? 0m, Settled = (decimal?)payment.Settled ?? 0m,
					Pending = (decimal?)payment.Pending ?? 0m, Applied = (decimal?)waiver.Applied ?? 0m,
					Reversed = (decimal?)waiver.Reversed ?? 0m
				},
				InvalidAllocation = ((int?)payment.Invalid ?? 0) > 0, InvalidWaiver = ((int?)waiver.Invalid ?? 0) > 0,
				InvalidRefund = ((int?)refund.InvalidCount ?? 0) > 0,
				Refunded = (decimal?)refund.Amount ?? 0m
			};
		return data.Select(FinanceExternalPositionExpressionExpansion.Expand(FinanceExternalPositionProjection.Row));
	}

	private sealed class PaymentTotal
	{
		public Guid ChargeId { get; init; }
		public decimal Accepted { get; init; }
		public decimal Settled { get; init; }
		public decimal Pending { get; init; }
		public int Invalid { get; init; }
	}
	private sealed class WaiverTotal
	{
		public Guid ChargeId { get; init; }
		public decimal Applied { get; init; }
		public decimal Reversed { get; init; }
		public int Invalid { get; init; }
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Library;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.ExternalReceivables;

[ExposeServices(typeof(IFinanceExternalPositionReader))]
public class FinanceExternalPositionReader(
	IRepository<FinanceExternalReceivableSourceLink, Guid> links,
	IRepository<Charge, Guid> charges,
	IRepository<ChargeCollectionRecord, Guid> allocations,
	IRepository<FeeRecordMetadata, Guid> collections,
	IRepository<ChargeWaiverApplication, Guid> waivers,
	IRepository<FinanceChargeWaiverReversal, Guid> reversals,
	IFinanceExternalRefundEvidenceQuery refundEvidence,
	ICurrentTenant tenant, IAsyncQueryableExecuter queries) : DomainService, IFinanceExternalPositionReader
{
	public async Task<IReadOnlyList<FinanceExternalSourcePosition>> ReadAsync(
		IReadOnlyList<FinanceExternalPositionSource> authorizedSources,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (!tenant.Id.HasValue || authorizedSources.Count is < 1 or > 100 ||
			authorizedSources.Any(source => source.TenantId != tenant.Id || source.Kind != FinanceExternalSourceKind.LibraryAssessment || source.SourceId == Guid.Empty) ||
			authorizedSources.Select(source => source.SourceId).Distinct().Count() != authorizedSources.Count)
			throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource, "Read a bounded selection of current-school source facts.");
		var sourceIds = authorizedSources.Select(source => source.SourceId).ToArray();
		var sourceLinks = await links.GetListAsync(row => row.TenantId == tenant.Id && row.SourceKind == FinanceExternalSourceKind.LibraryAssessment &&
			Enumerable.Contains(sourceIds, row.SourceId), includeDetails: false, cancellationToken: cancellationToken);
		var chargeIds = sourceLinks.Select(row => row.FinanceChargeId).Distinct().ToArray();
		var chargeRows = await charges.GetListAsync(row => row.TenantId == tenant.Id && Enumerable.Contains(chargeIds, row.Id), false, cancellationToken);
		var paymentRows = await allocations.GetListAsync(row => row.TenantId == tenant.Id && Enumerable.Contains(chargeIds, row.ChargeId), false, cancellationToken);
		var headerIds = paymentRows.Select(row => row.FeeRecordMetadataId).Distinct().ToArray();
		var headers = await collections.GetListAsync(row => row.TenantId == tenant.Id && Enumerable.Contains(headerIds, row.Id), false, cancellationToken);
		var applied = await waivers.GetListAsync(row => row.TenantId == tenant.Id && Enumerable.Contains(chargeIds, row.ChargeId), false, cancellationToken);
		var waiverIds = applied.Select(row => row.Id).ToArray();
		var reversed = await reversals.GetListAsync(row => row.TenantId == tenant.Id && Enumerable.Contains(waiverIds, row.WaiverApplicationId), false, cancellationToken);
		var refundChargeIds = (await charges.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id &&
			Enumerable.Contains(chargeIds, row.Id)).Select(row => row.Id);
		var refundTotals = await queries.ToListAsync(await refundEvidence.GetQueryAsync(refundChargeIds, cancellationToken), cancellationToken);
		var bySource = sourceLinks.ToDictionary(row => row.SourceId);
		var byCharge = chargeRows.ToDictionary(row => row.Id);
		var results = new List<FinanceExternalSourcePosition>(authorizedSources.Count);
		foreach (var source in authorizedSources)
		{
			cancellationToken.ThrowIfCancellationRequested();
			bySource.TryGetValue(source.SourceId, out var link);
			Charge? charge = null;
			if (link != null) byCharge.TryGetValue(link.FinanceChargeId, out charge);
			var accepted = link == null ? new FinanceExternalAcceptedEvidence() : FinanceExternalAcceptedEvidenceReader.Read(link, charge);
			FinanceExternalBalanceInput balance = new();
			var invalid = false;
			if (charge != null)
			{
				try
				{
					var chargeWaivers = applied.Where(row => row.ChargeId == charge.Id).ToArray();
					var currentIds = chargeWaivers.Select(row => row.Id).ToHashSet();
					var amounts = FinanceExternalChargeBalanceReader.Calculate(charge,
						paymentRows.Where(row => row.ChargeId == charge.Id).ToArray(), headers, chargeWaivers,
						reversed.Where(row => currentIds.Contains(row.WaiverApplicationId)).ToArray()).Amounts;
					balance = new() { Original = amounts.Original, Accepted = amounts.Accepted, Settled = amounts.Settled,
						Pending = amounts.PendingVerification, Applied = chargeWaivers.Sum(row => row.EffectiveAmount),
						Reversed = reversed.Where(row => currentIds.Contains(row.WaiverApplicationId)).Sum(row => row.Amount), Status = charge.Status };
				}
				catch (BusinessException error) when (error.Code is LibraryFinanceErrorCodes.InvalidBalance or LibraryFinanceErrorCodes.InvalidAmount)
				{ invalid = true; }
			}
			var refund = charge == null ? null : refundTotals.FirstOrDefault(value => value.ChargeId == charge.Id);
			var row = FinanceExternalPositionProjection.Project(new()
			{
				Source = source, Accepted = accepted, Balance = balance, InvalidAllocation = invalid,
				InvalidRefund = refund?.InvalidCount > 0,
				CurrentReason = FinanceExternalAcceptedEvidenceReader.NormalizeReason(source.CurrentLegacyResolvedReason),
				Refunded = refund?.Amount ?? 0m
			});
			results.Add(new(row.SourceId, row.SourceLinkId, row.AcademicYearId, row.Evidence, row.RecordedSettlementDeclaration,
				row.AssessmentOriginal, row.Currency, row.ApprovalStatus,
				row.Evidence == FinanceExternalPositionEvidence.Verified ? new(row.Original!.Value, row.Accepted!.Value,
					row.Settled!.Value, row.Pending!.Value, row.NetWaived!.Value, row.FinancialOutstanding!.Value, row.Collectible!.Value, row.Refunded!.Value) : null,
				row.ReviewCode == null ? [] : ["The source or financial evidence requires review before a balance can be confirmed."]));
		}
		return results;
	}
}

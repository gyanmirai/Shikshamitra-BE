using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Mobile;

/// <summary>Canonical family charge facts. Callers must first require the persisted child/year association.</summary>
[RemoteService(false)]
public class MobileFamilyChargeReader(IChargeRepository charges, IRepository<ChargeCategory, Guid> categories,
	IRepository<ChargeCollectionRecord, Guid> allocations, IRepository<FeeRecordMetadata, Guid> transactions,
	IRepository<ChargeWaiverApplication, Guid> waivers, IRepository<FeeRefundRequest, Guid> refunds)
	: ApplicationService, ITransientDependency
{
	internal async Task<List<FamilyChargeFacts>> ReadAsync(Guid studentId, Guid yearId, CancellationToken ct)
	{
		var tenantId = CurrentTenant.Id;
		var rows = await charges.GetListAsync(x => x.TenantId == tenantId && x.StudentId == studentId && x.AcademicYearId == yearId
			&& (x.Status == ChargeStatus.Approved || x.Status == ChargeStatus.Voided), cancellationToken: ct);
		if (rows.Count == 0) return [];
		var ids = rows.Select(x => x.Id).ToArray();
		var categoryIds = rows.Select(x => x.ChargeCategoryId).Distinct().ToArray();
		var names = (await categories.GetListAsync(x => x.TenantId == tenantId && x.AcademicYearId == yearId
			&& Enumerable.Contains(categoryIds, x.Id), cancellationToken: ct)).ToDictionary(x => x.Id, x => x.Name);
		var ownedTransactions = (await transactions.GetQueryableAsync()).Where(x => x.TenantId == tenantId && x.StudentId == studentId && x.AcademicYearId == yearId);
		var payments = await AsyncExecuter.ToListAsync(
			from allocation in await allocations.GetQueryableAsync()
			join transaction in ownedTransactions on allocation.FeeRecordMetadataId equals transaction.Id
			where allocation.TenantId == tenantId && allocation.StudentId == studentId && Enumerable.Contains(ids, allocation.ChargeId)
			select new FamilyChargeAllocation(allocation.Id, allocation.ChargeId, transaction.Id, allocation.PaymentDate, allocation.Amount, transaction.Status), ct);
		var waiverRows = await waivers.GetListAsync(x => x.TenantId == tenantId && x.StudentId == studentId && x.AcademicYearId == yearId
			&& Enumerable.Contains(ids, x.ChargeId), cancellationToken: ct);
		var ownedRefunds = (await refunds.GetQueryableAsync()).Where(x => x.TenantId == tenantId && x.StudentId == studentId && x.AcademicYearId == yearId
			&& (x.Status == FeeRefundStatus.Processed || x.Status == FeeRefundStatus.Reversed));
		var outcomes = await AsyncExecuter.ToListAsync(ownedRefunds.SelectMany(request => request.Lines, (request, line) =>
			new { request.Id, request.Status, line.ChargeId, line.SourceAllocationId, line.ProcessedAmount, line.ReversedAmount })
			.Where(line => line.ChargeId.HasValue && Enumerable.Contains(ids, line.ChargeId.Value) && line.ProcessedAmount > 0)
			.Select(line => new FamilyChargeRefund(line.Id, line.Status, line.ChargeId!.Value, line.SourceAllocationId,
				line.ProcessedAmount, line.ReversedAmount)), ct);
		// A supplied source allocation must belong to this exact child, year and charge.
		outcomes = outcomes.Where(r => !r.AllocationId.HasValue || payments.Any(p => p.Id == r.AllocationId && p.ChargeId == r.ChargeId)).ToList();
		return rows.Select(row => new FamilyChargeFacts(row, names.GetValueOrDefault(row.ChargeCategoryId, "Category unavailable"),
			payments.Where(p => p.ChargeId == row.Id).ToList(), waiverRows.Where(w => w.ChargeId == row.Id).Sum(w => w.EffectiveAmount),
			outcomes.Where(r => r.ChargeId == row.Id).ToList())).ToList();
	}
}

internal record FamilyChargeAllocation(Guid Id, Guid ChargeId, Guid TransactionId, DateTimeOffset Date, double LegacyAmount, FeeCollectionStatus Status)
{
	public decimal Amount => MobileFinanceProjection.LegacyMoney(LegacyAmount);
}
internal record FamilyChargeRefund(Guid Id, FeeRefundStatus Status, Guid ChargeId, Guid? AllocationId, decimal Processed, decimal Reversed)
{
	public decimal Net => Math.Max(0m, Processed - Reversed);
}
internal record FamilyChargeFacts(Charge Charge, string CategoryName, List<FamilyChargeAllocation> Payments, decimal Waived, List<FamilyChargeRefund> Refunds)
{
	public decimal Verified => Payments.Where(p => p.Status != FeeCollectionStatus.Reversed && p.Status != FeeCollectionStatus.InReconciliation).Sum(p => p.Amount);
	public decimal Pending => Payments.Where(p => p.Status == FeeCollectionStatus.InReconciliation).Sum(p => p.Amount);
	public decimal Outstanding => Charge.Status == ChargeStatus.Voided ? 0m : Math.Max(0m, Charge.Amount - Verified - Pending - Waived);
	public decimal Refunded => Refunds.Sum(r => r.Net);
	public bool Open => Charge.Status == ChargeStatus.Approved && (Outstanding > 0m || Pending > 0m);
	public bool Settled => Charge.Status == ChargeStatus.Approved && Outstanding == 0m && Pending == 0m;
	public MobileChargeBalance Legacy => new(Charge.Id, Charge.ChargeName, Charge.PurposeDescription, Charge.Amount, Verified + Pending, Waived, Pending);
	public MobileFamilyCharge ToDto(DateTimeOffset now)
	{
		var dueDate = DateOnly.FromDateTime(Charge.DueDate.ToOffset(TimeSpan.FromMinutes(330)).DateTime);
		var today = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromMinutes(330)).DateTime);
		var status = Charge.Status == ChargeStatus.Voided ? "Voided" : Pending > 0m ? "InReconciliation"
			: Outstanding > 0m ? Verified > 0m ? "Partial" : "Pending"
			: Waived > 0m ? Verified > 0m ? "Settled" : "Waived" : "Paid";
		return new(Charge.Id, Charge.ChargeName, Charge.PurposeDescription, Charge.ChargeCategoryId, CategoryName,
			dueDate, Charge.AppliedOn.ToUniversalTime(), Charge.ApprovedOn?.ToUniversalTime(), Charge.VoidedOn?.ToUniversalTime(),
			Charge.Status.ToString(), status, Charge.Status == ChargeStatus.Approved && Outstanding > 0m && dueDate < today,
			MobileFinanceProjection.Money(Charge.Amount), MobileFinanceProjection.Money(Verified), MobileFinanceProjection.Money(Pending),
			MobileFinanceProjection.Money(Waived), MobileFinanceProjection.Money(Outstanding), MobileFinanceProjection.Money(Refunded));
	}
}

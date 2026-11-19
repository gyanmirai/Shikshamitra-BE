using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Receipts;
using Volo.Abp;
using Volo.Abp.Authorization;
using Permissions = ShikshaMitra.Finance.Mobile.MobileFinancePermissions;

namespace ShikshaMitra.Finance.Mobile;

public partial class MobileFinanceReadService
{
	private Task<List<FamilyChargeFacts>> FamilyChargeRowsAsync(Guid studentId, Guid yearId, CancellationToken ct) =>
		LazyServiceProvider.LazyGetRequiredService<MobileFamilyChargeReader>().ReadAsync(studentId, yearId, ct);
	[Authorize(Permissions.ViewCharges)]
	public async Task<MobileChargePage> ChargesPageAsync(Guid studentId, Guid academicYearId, int page = 1, int pageSize = 20,
		string filter = "all", string? search = null, Guid? chargeCategoryId = null, CancellationToken cancellationToken = default)
	{
		await access.RequireAsync(studentId, academicYearId, permission: Permissions.ViewCharges, cancellationToken: cancellationToken);
		ValidatePage(page, pageSize); search = search?.Trim();
		if (filter is not ("all" or "open" or "settled" or "voided") || search?.Length > 100 || chargeCategoryId == Guid.Empty)
			throw new BusinessException("Finance:SelfService:InvalidRequest");
		var rows = await FamilyChargeRowsAsync(studentId, academicYearId, cancellationToken);
		var categories = rows.Select(x => new MobileChargeCategory(x.Charge.ChargeCategoryId, x.CategoryName)).Distinct()
			.OrderBy(x => x.Name).ThenBy(x => x.Id).ToList();
		var matches = rows.Where(x => (!chargeCategoryId.HasValue || x.Charge.ChargeCategoryId == chargeCategoryId)
			&& (string.IsNullOrEmpty(search) || x.Charge.ChargeName.Contains(search, StringComparison.OrdinalIgnoreCase)
				|| x.CategoryName.Contains(search, StringComparison.OrdinalIgnoreCase)
				|| x.Charge.PurposeDescription.Contains(search, StringComparison.OrdinalIgnoreCase))).ToList();
		var counts = new MobileChargeCounts(matches.Count, matches.Count(x => x.Open), matches.Count(x => x.Settled), matches.Count(x => x.Charge.Status == ChargeStatus.Voided));
		var approved = matches.Where(x => x.Charge.Status == ChargeStatus.Approved).ToList();
		var summary = new MobileChargeSummary(MobileFinanceProjection.Money(approved.Sum(x => x.Charge.Amount)), MobileFinanceProjection.Money(approved.Sum(x => x.Verified + x.Pending)), MobileFinanceProjection.Money(approved.Sum(x => x.Verified)),
			MobileFinanceProjection.Money(approved.Sum(x => x.Pending)), MobileFinanceProjection.Money(approved.Sum(x => x.Waived)),
			MobileFinanceProjection.Money(approved.Sum(x => x.Outstanding)), MobileFinanceProjection.Money(approved.Sum(x => x.Refunded)));
		var filtered = matches.Where(x => filter switch { "open" => x.Open, "settled" => x.Settled, "voided" => x.Charge.Status == ChargeStatus.Voided, _ => true }).ToList();
		var now = new DateTimeOffset(clock.Now).ToUniversalTime();
		return new(studentId, academicYearId, now, filtered.OrderByDescending(x => x.Charge.AppliedOn).ThenByDescending(x => x.Charge.Id)
			.Skip(Skip(page, pageSize)).Take(pageSize).Select(x => x.ToDto(now)).ToList(), filtered.Count, page, pageSize, summary, counts, categories);
	}
	[Authorize(Permissions.ViewCharges)]
	public async Task<MobileChargeDetail> ChargeAsync(Guid studentId, Guid academicYearId, Guid chargeId, CancellationToken cancellationToken = default)
	{
		await access.RequireAsync(studentId, academicYearId, permission: Permissions.ViewCharges, cancellationToken: cancellationToken);
		var charge = await RequireFamilyChargeAsync(studentId, academicYearId, chargeId, cancellationToken);
		var now = new DateTimeOffset(clock.Now).ToUniversalTime(); return new(studentId, academicYearId, now, charge.ToDto(now));
	}
	private async Task<FamilyChargeFacts> RequireFamilyChargeAsync(Guid studentId, Guid yearId, Guid chargeId, CancellationToken ct) =>
		(await FamilyChargeRowsAsync(studentId, yearId, ct)).SingleOrDefault(x => x.Charge.Id == chargeId) ?? throw new AbpAuthorizationException();
	[Authorize(Permissions.ViewCharges)]
	public async Task<MobileChargePaymentPage> ChargePaymentsAsync(Guid studentId, Guid academicYearId, Guid chargeId,
		int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
	{
		await access.RequireAsync(studentId, academicYearId, permission: Permissions.ViewCharges, cancellationToken: cancellationToken);
		ValidatePage(page, pageSize);
		var charge = await RequireFamilyChargeAsync(studentId, academicYearId, chargeId, cancellationToken);
		var rows = charge.Payments.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).Skip(Skip(page, pageSize)).Take(pageSize).ToList();
		var txIds = rows.Select(x => x.TransactionId).Distinct().ToArray(); var tenantId = CurrentTenant.Id;
		var splits = await methods.GetListAsync(x => x.TenantId == tenantId && x.StudentId == studentId && Enumerable.Contains(txIds, x.FeeRecordMetadataId), cancellationToken: cancellationToken);
		var documents = await access.IsGrantedAsync(Permissions.ViewReceipts)
			? await receipts.GetListAsync(x => x.TenantId == tenantId && x.StudentId == studentId && x.AcademicYearId == academicYearId
				&& Enumerable.Contains(txIds, x.FeeCollectionTransactionId) && (x.ReceiptScope == FeeReceiptScope.Charge || x.ReceiptScope == FeeReceiptScope.Combined), cancellationToken: cancellationToken)
			: new List<FeePaymentReceipt>();
		var linkIds = documents.SelectMany(x => new[] { x.LinkedFinalReceiptId, x.LinkedProvisionalReceiptId }).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
		var ownedTransactions = (await transactions.GetQueryableAsync()).Where(x => x.TenantId == tenantId && x.StudentId == studentId && x.AcademicYearId == academicYearId).Select(x => x.Id);
		var ownedLinks = (await AsyncExecuter.ToListAsync((await receipts.GetQueryableAsync()).Where(x => x.TenantId == tenantId && x.StudentId == studentId && x.AcademicYearId == academicYearId
			&& Enumerable.Contains(linkIds, x.Id) && ownedTransactions.Contains(x.FeeCollectionTransactionId)
			&& (x.ReceiptScope == FeeReceiptScope.Charge || x.ReceiptScope == FeeReceiptScope.Combined)).Select(x => x.Id), cancellationToken)).ToHashSet();
		var canViewRefunds = await access.IsGrantedAsync(Permissions.ViewRefunds);
		return new(studentId, academicYearId, chargeId, rows.Select(row =>
		{
			var refunds = charge.Refunds.Where(x => x.AllocationId == row.Id).ToList();
			var refs = canViewRefunds ? refunds.GroupBy(x => new { x.Id, x.Status }).Select(g => new MobileChargeRefund(g.Key.Id, g.Key.Status.ToString(), MobileFinanceProjection.Money(g.Sum(x => x.Net)))).ToList() : [];
			return new MobileChargePayment(row.Id, row.TransactionId, row.Date.ToUniversalTime(), MobileFinanceProjection.Money(row.Amount), row.Status.ToString(),
				splits.Where(x => x.FeeRecordMetadataId == row.TransactionId).Select(x => x.PaymentMethod.ToString()).Distinct().ToList(), MobileFinanceProjection.Money(refunds.Sum(x => x.Net)),
				documents.Where(x => x.FeeCollectionTransactionId == row.TransactionId).Select(x =>
				{
					var link = x.LinkedFinalReceiptId ?? x.LinkedProvisionalReceiptId;
					return new MobileChargeReceipt(x.Id, x.FeeCollectionTransactionId, x.ReceiptNumber, x.ReceiptKind.ToString(), x.VerificationStatus.ToString(),
						x.ReceiptDate, MobileFinanceProjection.Money(MobileFinanceProjection.LegacyMoney(x.TotalPaidAmount)), x.LinkedFinalReceiptId.HasValue && ownedLinks.Contains(x.LinkedFinalReceiptId.Value) ? x.LinkedFinalReceiptId : null, x.LinkedProvisionalReceiptId.HasValue && ownedLinks.Contains(x.LinkedProvisionalReceiptId.Value) ? x.LinkedProvisionalReceiptId : null, x.ReceiptScope.ToString(), true);
				}).ToList(), refs);
		}).ToList(), charge.Payments.Count, page, pageSize);
	}
}

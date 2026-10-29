using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Mobile;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;
using Permissions = ShikshaMitra.Finance.Mobile.MobileFinancePermissions;

namespace ShikshaMitra.Academic.Mobile;

public partial class MobileAcademicAppService_Tests
{
	[Fact]
	public void Finance_transactions_contract_exposes_combined_history()
	{
		typeof(IMobileFinanceReadService).GetMethod("TransactionsAsync").ShouldNotBeNull();
	}

	[Fact]
	public Task Finance_transactions_collection_permission_scope_receipts_and_exact_amounts() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, true); using var identity = FinanceIdentity(f.User.Id);
		var reads = RealFinanceReads();
		await Should.ThrowAsync<AbpAuthorizationException>(() => reads.TransactionsAsync(f.Student.Id, f.Year.Id));
		await GrantFinancePermissionAsync(f.User.Id, Permissions.View);
		await Should.ThrowAsync<AbpAuthorizationException>(() => reads.TransactionsAsync(Guid.NewGuid(), f.Year.Id));
		await Should.ThrowAsync<AbpAuthorizationException>(() => reads.TransactionsAsync(f.Student.Id, Guid.NewGuid()));
		var date = new DateTime(2026, 10, 8, 12, 30, 0, DateTimeKind.Utc);
		var tx = new FeeRecordMetadata(Guid.NewGuid(), tenantId, f.Student.Id, f.Year.Id, 0.30, "COMBINED", FeeCollectionStatus.Paid);
		SetHistoricalProperty(tx, "CreationTime", date); await Insert(tx);
		await Insert(PaymentMethodRecord.Create(f.Student.Id, tx.Id, 0.10m, new DateTimeOffset(date), PaymentMethodEnum.Cash, tenantId: tenantId));
		await Insert(PaymentMethodRecord.Create(f.Student.Id, tx.Id, 0.20m, new DateTimeOffset(date), PaymentMethodEnum.Cash, tenantId: tenantId));
		var receipt = await Insert(new FeePaymentReceipt(Guid.NewGuid()) { TenantId = tenantId, StudentId = f.Student.Id, AcademicYearId = f.Year.Id,
			FeeCollectionTransactionId = tx.Id, ReceiptNumber = "ACTUAL-COMBINED", ReceiptKind = FeeReceiptKind.FinalFee, ReceiptScope = FeeReceiptScope.Combined,
			TotalPaidAmount = 0.30, ReceiptDate = DateOnly.FromDateTime(date) });
		var page = await reads.TransactionsAsync(f.Student.Id, f.Year.Id);
		page.TotalCount.ShouldBe(1); var row = page.Items.ShouldHaveSingleItem();
		row.TransactionId.ShouldBe($"payment:{tx.Id:D}"); row.PaymentId.ShouldBe(tx.Id);
		row.Kind.ShouldBe("payment"); row.Direction.ShouldBe("in"); row.Status.ShouldBe("Paid");
		row.Amount.ShouldBe("0.30"); row.OccurredAt.ShouldBe(new DateTimeOffset(date)); row.Date.ShouldBe(DateOnly.FromDateTime(date));
		row.PaymentMethods.ShouldHaveSingleItem().ShouldBe("Cash"); row.Receipts.ShouldBeEmpty();
		page.PaymentSummary!.PaidAmount.ShouldBe("0.30");
		await GrantFinancePermissionAsync(f.User.Id, Permissions.ViewReceipts);
		(await reads.TransactionsAsync(f.Student.Id, f.Year.Id)).Items[0].Receipts.ShouldHaveSingleItem().ReceiptId.ShouldBe(receipt.Id);
		(await reads.TransactionsAsync(f.Student.Id, f.Year.Id, 1, 1)).Items.ShouldBeEmpty();
		foreach (var offset in new[] { -1, int.MaxValue })
			await Should.ThrowAsync<BusinessException>(() => reads.TransactionsAsync(f.Student.Id, f.Year.Id, offset));
		await Should.ThrowAsync<BusinessException>(() => reads.TransactionsAsync(f.Student.Id, f.Year.Id, maxResultCount: 101));
		await GetRequiredService<IRepository<ShikshaMitra.Students.Portal.StudentPortalAccountLink, Guid>>().DeleteAsync(f.Link, true);
		await Should.ThrowAsync<AbpAuthorizationException>(() => reads.TransactionsAsync(f.Student.Id, f.Year.Id));
	});

	[FamilyFinancePostgresFact]
	public Task PostgreSQL_family_transactions_union_refunds_returns_legacy_and_global_page() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, true); using var identity = FinanceIdentity(f.User.Id);
		await GrantFinancePermissionAsync(f.User.Id, Permissions.View);
		var paymentAt = DateTimeOffset.Parse("2026-10-09T10:00:00Z");
		var postedAt = paymentAt.AddDays(1);
		foreach (var amount in new[] { 0.30m, 1000.00m })
		{
			var tx = new FeeRecordMetadata(Guid.NewGuid(), tenantId, f.Student.Id, f.Year.Id, (double)amount, $"PAY-{amount}", FeeCollectionStatus.Paid);
			SetHistoricalProperty(tx, "CreationTime", paymentAt.UtcDateTime); await Insert(tx);
			await Insert(PaymentMethodRecord.Create(f.Student.Id, tx.Id, amount, paymentAt, PaymentMethodEnum.Cash, tenantId: tenantId));
		}
		var posted = await TransactionRefundAsync(f, tenantId, 50.25m, FeeRefundSettlementStatus.Reversed, postedAt);
		var outgoing = new FeeRefundAccountingEntry(Guid.NewGuid(), tenantId, posted.Request.Id, posted.Settlement.Id, null, null,
			"Cash", "PRIVATE-REFERENCE", 50.25m, false, null, "[]", "PRIVATE-EVIDENCE", f.User.Id);
		SetHistoricalProperty(outgoing, "PostedAt", postedAt); await Insert(outgoing);
		var returned = new FeeRefundAccountingEntry(Guid.NewGuid(), tenantId, posted.Request.Id, posted.Settlement.Id, null, null,
			"Cash", "PRIVATE-REFERENCE", 50.25m, true, outgoing.Id, "[]", "PRIVATE-EVIDENCE", f.User.Id);
		SetHistoricalProperty(returned, "PostedAt", postedAt.AddDays(1)); await Insert(returned);
		var legacy = await TransactionRefundAsync(f, tenantId, 20.10m, FeeRefundSettlementStatus.Processed, postedAt);
		await TransactionRefundAsync(f, tenantId, 99m, FeeRefundSettlementStatus.Failed, postedAt.AddDays(5));
		await TransactionRefundAsync(f, tenantId, 88m, FeeRefundSettlementStatus.Processing, postedAt.AddDays(5));
		await TransactionRefundAsync(f, tenantId, 77m, FeeRefundSettlementStatus.Processed, postedAt, completed: false);
		var foreign = await TransactionRefundAsync(f, tenantId, 66m, FeeRefundSettlementStatus.Processed, postedAt, owner: Guid.NewGuid());
		await Insert(new FeeRefundAccountingEntry(Guid.NewGuid(), tenantId, foreign.Request.Id, foreign.Settlement.Id, null, null,
			"Cash", null, 66m, false, null, "[]", "private", f.User.Id));
		var reads = RealFinanceReads();
		(await reads.TransactionsAsync(f.Student.Id, f.Year.Id)).TotalCount.ShouldBe(2, "Refund View is independently required.");
		await GrantFinancePermissionAsync(f.User.Id, Permissions.ViewRefunds);
		var all = await reads.TransactionsAsync(f.Student.Id, f.Year.Id);
		all.TotalCount.ShouldBe(5); all.Items.Count.ShouldBe(5);
		all.Items.Select(x => x.TransactionId).Distinct().Count().ShouldBe(5);
		all.Items[0].TransactionId.ShouldBe($"refundReturn:{returned.Id:D}"); all.Items[0].Direction.ShouldBe("in");
		all.Items[0].Amount.ShouldBe("50.25"); all.Items[0].Status.ShouldBe("Reversed");
		var payout = all.Items.Single(x => x.TransactionId == $"refund:{posted.Settlement.Id:D}");
		payout.Amount.ShouldBe("50.25"); payout.Direction.ShouldBe("out"); payout.OccurredAt.ShouldBe(postedAt);
		payout.Date.ShouldBe(DateOnly.FromDateTime(posted.Settlement.SettlementDate.Date));
		all.Items.Single(x => x.TransactionId == $"refund:{legacy.Settlement.Id:D}").Amount.ShouldBe("20.10");
		all.Items.Where(x => x.Kind != "payment").ShouldAllBe(x => x.Receipts.Count == 0 && x.PaymentId == null);
		all.PaymentSummary!.PaidAmount.ShouldBe("1000.30"); all.PaymentSummary.SuccessfulPaymentCount.ShouldBe(2);
		var page = await reads.TransactionsAsync(f.Student.Id, f.Year.Id, 1, 2);
		page.TotalCount.ShouldBe(5); page.Items.Select(x => x.TransactionId).ShouldBe(all.Items.Skip(1).Take(2).Select(x => x.TransactionId));
		page.PaymentSummary.ShouldBe(all.PaymentSummary);
		var tied = all.Items.Where(x => x.OccurredAt == postedAt).Select(x => x.TransactionId).ToList();
		tied.ShouldBe(tied.OrderBy(x => x, StringComparer.Ordinal).ToList());
		var json = JsonSerializer.Serialize(all, new JsonSerializerOptions(JsonSerializerDefaults.Web));
		json.ShouldNotContain("PRIVATE"); json.ShouldNotContain("beneficiary", Case.Insensitive); json.ShouldNotContain("bankAccount", Case.Insensitive);
		WriteFamilyChargeWireSample("transactions-persisted.json", all);
	});

	private async Task<(FeeRefundRequest Request, FeeRefundSettlement Settlement)> TransactionRefundAsync(Fixture f, Guid tenantId,
		decimal amount, FeeRefundSettlementStatus status, DateTimeOffset at, bool completed = true, Guid? owner = null)
	{
		var request = HistoricalRefund(f, tenantId, Guid.NewGuid(), Guid.NewGuid(), amount, 0m, FeeRefundStatus.Processed);
		if (owner.HasValue) SetHistoricalProperty(request, "StudentId", owner.Value);
		var settlement = (FeeRefundSettlement)Activator.CreateInstance(typeof(FeeRefundSettlement), BindingFlags.Instance | BindingFlags.NonPublic, null,
			[Guid.NewGuid(), request.Id, 1, f.User.Id, Guid.NewGuid().ToString("N"), "Cash", amount, at.AddDays(-2), "PRIVATE-REFERENCE", "{\"private\":\"PRIVATE-DETAILS\"}"], null)!;
		SetHistoricalProperty(settlement, "Status", status);
		if (completed) SetHistoricalProperty(settlement, "CompletedAt", at);
		((List<FeeRefundSettlement>)typeof(FeeRefundRequest).GetField("_settlements", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(request)!).Add(settlement);
		await Insert(request); return (request, settlement);
	}
}

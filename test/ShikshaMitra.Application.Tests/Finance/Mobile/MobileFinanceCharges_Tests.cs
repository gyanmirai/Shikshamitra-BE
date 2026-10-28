using System;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Mobile;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.Students.Portal;
using Xunit;
using Permissions = ShikshaMitra.Finance.Mobile.MobileFinancePermissions;

namespace ShikshaMitra.Academic.Mobile;

public partial class MobileAcademicAppService_Tests
{
	// Catches counting pending/reversed allocations as verified, or subtracting pending twice.
	[Fact]
	public Task Charges_page_separates_verified_pending_waived_and_preserves_legacy_paid() => ChargeEquation();
	[FamilyFinancePostgresFact]
	public Task PostgreSQL_family_charges_executes_owned_allocation_queries() => ChargeEquation();
	private Task ChargeEquation() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, true); using var identity = FinanceIdentity(f.User.Id);
		await GrantFinancePermissionAsync(f.User.Id, Permissions.ViewCharges);
		var category = await Insert(new ChargeCategory(Guid.NewGuid(), tenantId, f.Year.Id, "Library", false, null));
		var charge = await Insert(NewFamilyCharge(f, tenantId, category.Id, "Damaged book", 1000m));
		await Insert(new ChargeWaiverApplication(Guid.NewGuid(), tenantId, f.Student.Id, charge.Id, f.Year.Id, 1000m, 100m, "Private waiver notes", f.User.Id, Guid.NewGuid().ToString("N")[..18]));
		foreach (var pair in new[] { (200d, FeeCollectionStatus.Paid), (300d, FeeCollectionStatus.InReconciliation), (700d, FeeCollectionStatus.Reversed) })
		{
			var tx = await Insert(new FeeRecordMetadata(Guid.NewGuid(), tenantId, f.Student.Id, f.Year.Id, pair.Item1, Guid.NewGuid().ToString("N")[..18], pair.Item2));
			await Insert(new ChargeCollectionRecord(Guid.NewGuid()) { TenantId = tenantId, StudentId = f.Student.Id, ChargeId = charge.Id, FeeRecordMetadataId = tx.Id, Amount = pair.Item1, PaymentDate = DateTimeOffset.UtcNow });
			await Insert(PaymentMethodRecord.Create(f.Student.Id, tx.Id, (decimal)pair.Item1, DateTimeOffset.UtcNow, PaymentMethodEnum.Cash, tenantId: tenantId));
		}
		var foreignYearTx = await Insert(new FeeRecordMetadata(Guid.NewGuid(), tenantId, f.Student.Id, Guid.NewGuid(), 999d, Guid.NewGuid().ToString("N")[..18], FeeCollectionStatus.Paid));
		await Insert(new ChargeCollectionRecord(Guid.NewGuid()) { TenantId = tenantId, StudentId = f.Student.Id, ChargeId = charge.Id, FeeRecordMetadataId = foreignYearTx.Id, Amount = 999d, PaymentDate = DateTimeOffset.UtcNow });
		await Insert(new ChargeWaiverApplication(Guid.NewGuid(), tenantId, f.Student.Id, charge.Id, Guid.NewGuid(), 1000m, 999m, "Other year", f.User.Id, Guid.NewGuid().ToString("N")[..18]));
		// Expectations are hand-derived from the original obligation and persisted allocation outcomes.
		var reads = RealFinanceReads();
		var page = await reads.ChargesPageAsync(f.Student.Id, f.Year.Id);
		Math.Abs((page.AsOf - DateTimeOffset.UtcNow).TotalSeconds).ShouldBeLessThan(30d, "AsOf must convert the actual local or UTC clock instant, never relabel local wall time as UTC.");
		WriteFamilyChargeWireSample("charge-page.json", page);
		string outstanding = page.Items[0].OutstandingAmount; outstanding.ShouldBe("400.00");
		string verified = page.Items[0].VerifiedPaidAmount; verified.ShouldBe("200.00");
		string pending = page.Items[0].PendingVerificationAmount; pending.ShouldBe("300.00");
		string waived = page.Items[0].WaivedAmount; waived.ShouldBe("100.00");
		string status = page.Items[0].PaymentStatus; status.ShouldBe("InReconciliation");
		var history = await reads.ChargePaymentsAsync(f.Student.Id, f.Year.Id, charge.Id, pageSize: 1);
		history.TotalCount.ShouldBe(3); history.Items.Count.ShouldBe(1);
		history.Items[0].Status.ShouldBe("Reversed"); history.Items[0].Amount.ShouldBe("700.00");
		var legacy = (await RealFinanceReads().ChargesAsync(f.Student.Id, f.Year.Id)).Single();
		legacy.PaidAmount.ShouldBe("500.00"); legacy.OutstandingAmount.ShouldBe("400.00");
		await GrantFinancePermissionAsync(f.User.Id, Permissions.View);
		(await reads.OverviewAsync(f.Student.Id, f.Year.Id)).ChargeOutstandingAmount.ShouldBe("400.00");
		var statement = await reads.StatementPdfAsync(f.Student.Id, f.Year.Id);
		Encoding.ASCII.GetString(statement, 0, 5).ShouldBe("%PDF-");
		var serialized = JsonSerializer.Serialize(page);
		serialized.ShouldNotContain("Private"); serialized.ShouldNotContain("AdditionalNotes"); serialized.ShouldNotContain("RequestorId");
	});
	private static Charge NewFamilyCharge(Fixture f, Guid tenantId, Guid categoryId, string name, decimal amount) =>
		new(Guid.NewGuid(), tenantId, f.Year.Id, Guid.NewGuid(), name, categoryId, f.Student.Id,
			f.Student.AcademicBoardId!.Value, f.Student.AcademicMediumId, f.Enrollment.StandardId!.Value,
			f.Enrollment.DivisionId!.Value, "Public charge reason", amount, DateTimeOffset.UtcNow.AddDays(5),
			ChargePriority.Medium, ChargeStatus.Approved, f.User.Id, "Private staff notes");
	[Fact]
	public Task Charges_filters_counts_categories_and_voids_share_one_authorized_set() => ChargeFilters();
	[FamilyFinancePostgresFact]
	public Task PostgreSQL_family_charges_filters_counts_and_pages() => ChargeFilters();
	private Task ChargeFilters() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, false); using var identity = FinanceIdentity(f.User.Id);
		await GrantFinancePermissionAsync(f.User.Id, Permissions.ViewCharges);
		var category = await Insert(new ChargeCategory(Guid.NewGuid(), tenantId, f.Year.Id, "Library", false, null));
		var open = await Insert(NewFamilyCharge(f, tenantId, category.Id, "Book open", 100m));
		var waived = await Insert(NewFamilyCharge(f, tenantId, category.Id, "Book waived", 200m));
		await Insert(new ChargeWaiverApplication(Guid.NewGuid(), tenantId, f.Student.Id, waived.Id, f.Year.Id, 200m, 200m, "Private", f.User.Id, Guid.NewGuid().ToString("N")[..18]));
		var cancelled = NewFamilyCharge(f, tenantId, category.Id, "Book cancelled", 300m); cancelled.Void(Guid.NewGuid(), "Private cancellation reason"); await Insert(cancelled);
		var hidden = NewFamilyCharge(f, tenantId, category.Id, "Hidden pending", 999m);
		SetHistoricalProperty(hidden, "Status", ChargeStatus.Pending); await Insert(hidden);
		var sibling = NewFamilyCharge(f, tenantId, category.Id, "Sibling charge", 999m);
		SetHistoricalProperty(sibling, "StudentId", Guid.NewGuid()); await Insert(sibling);
		var reads = RealFinanceReads();
		var page = await reads.ChargesPageAsync(f.Student.Id, f.Year.Id, page: 1, pageSize: 1, filter: "all", search: " BOOK ", categoryId: category.Id);
		int total = page.TotalCount; total.ShouldBe(3);
		int all = page.Counts.All; all.ShouldBe(3); int openCount = page.Counts.Open; openCount.ShouldBe(1);
		int settled = page.Counts.Settled; settled.ShouldBe(1); int voided = page.Counts.Voided; voided.ShouldBe(1);
		string original = page.Summary.Amount; original.ShouldBe("300.00");
		string outstanding = page.Summary.OutstandingAmount; outstanding.ShouldBe("100.00");
		var second = await reads.ChargesPageAsync(f.Student.Id, f.Year.Id, page: 2, pageSize: 1);
		Guid firstId = page.Items[0].Id; Guid secondId = second.Items[0].Id; firstId.ShouldNotBe(secondId);
		var closed = await reads.ChargesPageAsync(f.Student.Id, f.Year.Id, filter: "settled");
		Guid closedId = closed.Items[0].Id; closedId.ShouldBe(waived.Id);
		string closedStatus = closed.Items[0].PaymentStatus; closedStatus.ShouldBe("Waived");
		var detail = await reads.ChargeAsync(f.Student.Id, f.Year.Id, cancelled.Id);
		string cancelledStatus = detail.Charge.PaymentStatus; cancelledStatus.ShouldBe("Voided");
		string cancelledOutstanding = detail.Charge.OutstandingAmount; cancelledOutstanding.ShouldBe("0.00");
		var empty = await reads.ChargesPageAsync(f.Student.Id, f.Year.Id, search: "no match");
		int categoryCount = empty.Categories.Count; categoryCount.ShouldBe(1);
		await Should.ThrowAsync<AbpAuthorizationException>(async () => await reads.ChargeAsync(f.Student.Id, f.Year.Id, hidden.Id));
		await Should.ThrowAsync<AbpAuthorizationException>(async () => await reads.ChargeAsync(f.Student.Id, f.Year.Id, sibling.Id));
		await Should.ThrowAsync<AbpAuthorizationException>(async () => await reads.ChargePaymentsAsync(f.Student.Id, f.Year.Id, sibling.Id));
		await Should.ThrowAsync<AbpAuthorizationException>(async () => await reads.ChargeAsync(f.Student.Id, f.Year.Id, open.ChargeRequestId));
		await Should.ThrowAsync<Volo.Abp.BusinessException>(() => reads.ChargesPageAsync(f.Student.Id, f.Year.Id, page: 0));
		await Should.ThrowAsync<Volo.Abp.BusinessException>(() => reads.ChargesPageAsync(f.Student.Id, f.Year.Id, pageSize: 101));
		await Should.ThrowAsync<Volo.Abp.BusinessException>(() => reads.ChargesPageAsync(f.Student.Id, f.Year.Id, filter: "pending"));
		await Should.ThrowAsync<Volo.Abp.BusinessException>(() => reads.ChargesPageAsync(f.Student.Id, f.Year.Id, search: new string('a', 101)));
	});
	[Fact]
	public Task Charges_history_returns_allocations_and_only_owned_charge_receipts_and_outcomes() => ChargeHistory();
	[FamilyFinancePostgresFact]
	public Task PostgreSQL_family_charges_history_joins_actual_receipts_and_refunds() => ChargeHistory();
	private Task ChargeHistory() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, true); using var identity = FinanceIdentity(f.User.Id);
		await GrantFinancePermissionAsync(f.User.Id, Permissions.ViewCharges);
		var charge = await Insert(NewFamilyCharge(f, tenantId, Guid.NewGuid(), "Charge history", 1000m));
		var tx = await Insert(new FeeRecordMetadata(Guid.NewGuid(), tenantId, f.Student.Id, f.Year.Id, 900d, "MIXED", FeeCollectionStatus.Paid));
		var allocation = await Insert(new ChargeCollectionRecord(Guid.NewGuid()) { TenantId = tenantId, StudentId = f.Student.Id, ChargeId = charge.Id, FeeRecordMetadataId = tx.Id, Amount = 200d, PaymentDate = DateTimeOffset.UtcNow });
		await Insert(PaymentMethodRecord.Create(f.Student.Id, tx.Id, 900m, DateTimeOffset.UtcNow, PaymentMethodEnum.Cash, tenantId: tenantId));
		FeePaymentReceipt Receipt(FeeReceiptScope scope, Guid? owner = null) => new(Guid.NewGuid()) { TenantId = tenantId, StudentId = owner ?? f.Student.Id, AcademicYearId = f.Year.Id, FeeCollectionTransactionId = tx.Id, ReceiptNumber = Guid.NewGuid().ToString("N")[..18], ReceiptKind = FeeReceiptKind.FinalFee, ReceiptScope = scope, TotalPaidAmount = scope == FeeReceiptScope.Combined ? 900d : 200d, ReceiptDate = DateOnly.FromDateTime(DateTime.UtcNow) };
		await Insert(Receipt(FeeReceiptScope.Fee));
		var actual = await Insert(Receipt(FeeReceiptScope.Charge));
		var combined = await Insert(Receipt(FeeReceiptScope.Combined));
		await Insert(Receipt(FeeReceiptScope.Charge, Guid.NewGuid()));
		var provisional = Receipt(FeeReceiptScope.Charge); provisional.ReceiptKind = FeeReceiptKind.ProvisionalCharge;
		provisional.LinkedFinalReceiptId = actual.Id; actual.LinkedProvisionalReceiptId = provisional.Id;
		await Insert(provisional); await GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().UpdateAsync(actual, true);
		var refund = HistoricalRefund(f, tenantId, charge.Id, allocation.Id, 50m, 0m, FeeRefundStatus.Processed); await Insert(refund);
		await Insert(HistoricalRefund(f, tenantId, charge.Id, allocation.Id, 30m, 30m, FeeRefundStatus.Reversed));
		await Insert(HistoricalRefund(f, tenantId, charge.Id, allocation.Id, 100m, 0m, FeeRefundStatus.Failed));
		var reads = RealFinanceReads(); var denied = await reads.ChargePaymentsAsync(f.Student.Id, f.Year.Id, charge.Id);
		int deniedReceipts = denied.Items[0].Receipts.Count; deniedReceipts.ShouldBe(0);
		int deniedRefunds = denied.Items[0].Refunds.Count; deniedRefunds.ShouldBe(0);
		string refundAmount = denied.Items[0].RefundedAmount; refundAmount.ShouldBe("50.00");
		await GrantFinancePermissionAsync(f.User.Id, Permissions.ViewReceipts); await GrantFinancePermissionAsync(f.User.Id, Permissions.ViewRefunds);
		var page = await reads.ChargePaymentsAsync(f.Student.Id, f.Year.Id, charge.Id);
		WriteFamilyChargeWireSample("charge-payments.json", page);
		Guid id = page.Items[0].Id; id.ShouldBe(allocation.Id); string amount = page.Items[0].Amount; amount.ShouldBe("200.00");
		int receiptCount = page.Items[0].Receipts.Count; receiptCount.ShouldBe(3);
		var receiptItems = page.Items[0].Receipts;
		string wholeTotal = receiptItems.Single(r => (Guid)r.Id == combined.Id).Amount; wholeTotal.ShouldBe("900.00");
		Guid? linked = receiptItems.Single(r => (Guid)r.Id == provisional.Id).LinkedReceiptId; linked.ShouldBe(actual.Id);
		int refundCount = page.Items[0].Refunds.Count; refundCount.ShouldBe(2);
		var detail = await reads.ChargeAsync(f.Student.Id, f.Year.Id, charge.Id);
		Math.Abs((detail.AsOf - DateTimeOffset.UtcNow).TotalSeconds).ShouldBeLessThan(30d);
		WriteFamilyChargeWireSample("charge-detail.json", detail);
		string payable = detail.Charge.OutstandingAmount; payable.ShouldBe("800.00", "Refunds do not reopen the charge.");
	});
	private static void SetHistoricalProperty(object entity, string name, object value) => entity.GetType().GetProperty(name)!.SetValue(entity, value);
	private static void WriteFamilyChargeWireSample(string fileName, object dto)
	{
		var directory = Environment.GetEnvironmentVariable("FAMILY_CHARGES_WIRE_OUTPUT");
		if (string.IsNullOrWhiteSpace(directory)) return;
		Directory.CreateDirectory(directory);
		File.WriteAllText(Path.Combine(directory, fileName), JsonSerializer.Serialize(new { success = true, data = dto },
			new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
	}
	private static FeeRefundRequest HistoricalRefund(Fixture f, Guid tenantId, Guid chargeId, Guid allocationId, decimal processed, decimal reversed, FeeRefundStatus status)
	{
		var requestId = Guid.NewGuid();
		var line = (FeeRefundLine)Activator.CreateInstance(typeof(FeeRefundLine), BindingFlags.Instance | BindingFlags.NonPublic, null,
			[Guid.NewGuid(), requestId, null, allocationId, null, null, "Charge", "", 200m, processed, processed, chargeId, null], null)!;
		SetHistoricalProperty(line, "ProcessedAmount", processed); SetHistoricalProperty(line, "ReversedAmount", reversed);
		var request = (FeeRefundRequest)Activator.CreateInstance(typeof(FeeRefundRequest), BindingFlags.Instance | BindingFlags.NonPublic, null,
			[requestId, tenantId, f.Student.Id, f.Year.Id, Guid.NewGuid().ToString("N")[..18], new[] { line }, null, null, "Private beneficiary reason", null], null)!;
		SetHistoricalProperty(request, "Status", status); return request;
	}
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public Task Charges_new_operations_enforce_live_principal_scope_year_and_denials(bool parent) => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, parent); using var identity = FinanceIdentity(f.User.Id);
		var reads = RealFinanceReads();
		await Should.ThrowAsync<AbpAuthorizationException>(async () => await reads.ChargesPageAsync(f.Student.Id, f.Year.Id));
		await GrantFinancePermissionAsync(f.User.Id, Permissions.ViewCharges);
		await reads.ChargesPageAsync(f.Student.Id, f.Year.Id);
		await Should.ThrowAsync<AbpAuthorizationException>(async () => await reads.ChargesPageAsync(Guid.NewGuid(), f.Year.Id));
		await Should.ThrowAsync<AbpAuthorizationException>(async () => await reads.ChargesPageAsync(f.Student.Id, Guid.NewGuid()));
		using (GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
			await Should.ThrowAsync<AbpAuthorizationException>(async () => await reads.ChargesPageAsync(f.Student.Id, f.Year.Id));
		await GetRequiredService<ShikshaMitra.Security.Permissions.IUserPermissionDenyStore>().SetDeniedAsync(f.User.Id, Permissions.ViewCharges, true);
		await Should.ThrowAsync<AbpAuthorizationException>(async () => await reads.ChargeAsync(f.Student.Id, f.Year.Id, Guid.NewGuid()));
		await GetRequiredService<ShikshaMitra.Security.Permissions.IUserPermissionDenyStore>().SetDeniedAsync(f.User.Id, Permissions.ViewCharges, false);
		await GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().DeleteAsync(f.Link, true);
		await Should.ThrowAsync<AbpAuthorizationException>(async () => await reads.ChargesPageAsync(f.Student.Id, f.Year.Id));
	});
	[Fact]
	public Task Charges_status_precedence_and_school_calendar_dates_are_consistent() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, false); using var identity = FinanceIdentity(f.User.Id);
		await GrantFinancePermissionAsync(f.User.Id, Permissions.ViewCharges);
		var reads = RealFinanceReads();
		foreach (var c in new[] {
			(Verified: 0m, Pending: 0m, Waived: 0m, Want: "Pending", Due: "1000.00"),
			(Verified: 250m, Pending: 0m, Waived: 0m, Want: "Partial", Due: "750.00"),
			(Verified: 1000m, Pending: 0m, Waived: 0m, Want: "Paid", Due: "0.00"),
			(Verified: 250m, Pending: 0m, Waived: 750m, Want: "Settled", Due: "0.00"),
			(Verified: 0m, Pending: 1000m, Waived: 0m, Want: "InReconciliation", Due: "0.00") })
		{
			var charge = NewFamilyCharge(f, tenantId, Guid.NewGuid(), c.Want, 1000m);
			SetHistoricalProperty(charge, "DueDate", DateTimeOffset.UtcNow.AddDays(-2)); await Insert(charge);
			foreach (var payment in new[] { (c.Verified, FeeCollectionStatus.Paid), (c.Pending, FeeCollectionStatus.InReconciliation) }.Where(x => x.Item1 > 0))
			{
				var transaction = await Insert(new FeeRecordMetadata(Guid.NewGuid(), tenantId, f.Student.Id, f.Year.Id, (double)payment.Item1, Guid.NewGuid().ToString("N")[..18], payment.Item2));
				await Insert(new ChargeCollectionRecord(Guid.NewGuid()) { TenantId = tenantId, StudentId = f.Student.Id, ChargeId = charge.Id, FeeRecordMetadataId = transaction.Id, Amount = (double)payment.Item1, PaymentDate = DateTimeOffset.UtcNow });
			}
			if (c.Waived > 0) await Insert(new ChargeWaiverApplication(Guid.NewGuid(), tenantId, f.Student.Id, charge.Id, f.Year.Id, 1000m, c.Waived, "Private", f.User.Id, Guid.NewGuid().ToString("N")[..18]));
			var detail = (await reads.ChargeAsync(f.Student.Id, f.Year.Id, charge.Id)).Charge;
			detail.PaymentStatus.ShouldBe(c.Want); detail.OutstandingAmount.ShouldBe(c.Due);
			detail.IsOverdue.ShouldBe(c.Due != "0.00"); detail.CategoryName.ShouldBe("Category unavailable");
		}
		var boundary = NewFamilyCharge(f, tenantId, Guid.NewGuid(), "School midnight", 100m);
		SetHistoricalProperty(boundary, "DueDate", new DateTimeOffset(2030, 1, 1, 20, 0, 0, TimeSpan.Zero)); await Insert(boundary);
		(await reads.ChargeAsync(f.Student.Id, f.Year.Id, boundary.Id)).Charge.DueDate.ShouldBe(new DateOnly(2030, 1, 2));
	});
}

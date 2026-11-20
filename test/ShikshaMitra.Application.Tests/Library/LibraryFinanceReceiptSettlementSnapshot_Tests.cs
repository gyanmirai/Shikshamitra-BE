using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Accounts.Reconciliation;
using ShikshaMitra.Finance.Accounts.Reconciliation.Dtos;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Receipt_Detail_Should_Expose_Its_Immutable_Pending_And_Final_Settlement_Snapshots_After_Actual_Bank_Verification()
	{
		var (context, collection) = await CollectTypedBankInstrumentAsync();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		using var actor = SignIn(context.Owner);
		await AssertSavedReceiptSnapshotAsync(collection.ReceiptId, settled: 0m, pending: 40.25m, outstanding: 100.75m, collectible: 60.50m);
		AssertReceiptSnapshot(await ReceiptDetailJsonAsync(context.Owner, collection.ReceiptId), 0m, 40.25m, 100.75m, 60.50m);
		using (SignIn(context.Reviewer))
		{
			var services = new DownstreamRefundServices(new FinanceServices(ServiceProvider, context.Reviewer, useStoredSource: true));
			var service = ActivatorUtilities.CreateInstance<ReconciliationAppService>(services);
			service.LazyServiceProvider = new AbpLazyServiceProvider(services);
			var approved = await WithUnitOfWorkAsync(() => service.ApproveAsync(collection.ReceiptId,
				new ApproveReconciliationInput { Remarks = "Verified actual bank evidence for immutable receipt presentation." }));
			approved.Success.ShouldBeTrue();
		}
		var finalId = await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().GetAsync(collection.ReceiptId)).LinkedFinalReceiptId!.Value);
		await AssertSavedReceiptSnapshotAsync(collection.ReceiptId, 0m, 40.25m, 100.75m, 60.50m);
		await AssertSavedReceiptSnapshotAsync(finalId, 40.25m, 0m, 60.50m, 60.50m);
		AssertReceiptSnapshot(await ReceiptDetailJsonAsync(context.Owner, collection.ReceiptId), 0m, 40.25m, 100.75m, 60.50m);
		AssertReceiptSnapshot(await ReceiptDetailJsonAsync(context.Owner, finalId), 40.25m, 0m, 60.50m, 60.50m);
	}

	[Fact]
	public async Task Receipt_Detail_Should_Keep_The_Original_Settlement_Position_After_A_Second_Actual_Partial_Payment()
	{
		var context = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		await ConfigureAndApproveTypedAsync(context);
		using var actor = SignIn(context.Owner);
		var form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
		var first = await WithUnitOfWorkAsync(() => TypedService(context.Owner).CollectAsync(CashCollectionInput(context, form.Revision)));
		await AssertSavedReceiptSnapshotAsync(first.ReceiptId, 40.25m, 0m, 60.50m, 60.50m);
		AssertReceiptSnapshot(await ReceiptDetailJsonAsync(context.Owner, first.ReceiptId), 40.25m, 0m, 60.50m, 60.50m);
		form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
		var secondInput = CashCollectionInput(context, form.Revision);
		secondInput.Payments[0].Amount = 20.25m;
		var second = await WithUnitOfWorkAsync(() => TypedService(context.Owner).CollectAsync(secondInput));
		second.Receivable.SettledPaymentAmount.ShouldBe(60.50m);
		second.Receivable.OutstandingAmount.ShouldBe(40.25m);
		AssertReceiptSnapshot(await ReceiptDetailJsonAsync(context.Owner, first.ReceiptId), 40.25m, 0m, 60.50m, 60.50m);
		var newer = await ReceiptDetailJsonAsync(context.Owner, second.ReceiptId);
		newer.TryGetProperty("settlementSnapshot", out var snapshot).ShouldBeTrue();
		snapshot.GetProperty("submittedAmount").GetDecimal().ShouldBe(20.25m);
		snapshot.GetProperty("settledAmount").GetDecimal().ShouldBe(60.50m);
		snapshot.GetProperty("financialOutstandingAmount").GetDecimal().ShouldBe(40.25m);
	}

	[Fact]
	public async Task Receipt_Detail_Should_Not_Invent_A_Settlement_Snapshot_For_Historical_Receipt_Evidence_That_Did_Not_Capture_One()
	{
		var context = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		await ConfigureAndApproveTypedAsync(context);
		using var actor = SignIn(context.Owner);
		var form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
		var collection = await WithUnitOfWorkAsync(() => TypedService(context.Owner).CollectAsync(CashCollectionInput(context, form.Revision)));
		await WithUnitOfWorkAsync(async () =>
		{
			// Historical input only: the real payment/header/source remain unchanged.
			// Older receipts did not capture this optional display evidence.
			var receipts = GetRequiredService<IRepository<FeePaymentReceipt, Guid>>();
			var headers = GetRequiredService<IRepository<FeeRecordMetadata, Guid>>();
			var receipt = await receipts.GetAsync(collection.ReceiptId);
			var header = await headers.GetAsync(collection.CollectionId);
			receipt.ExtraProperties.Remove("ReceiptSettlementSnapshotV1").ShouldBeTrue();
			header.ExtraProperties.Remove("ReceiptSettlementSnapshotV1").ShouldBeTrue();
			await receipts.UpdateAsync(receipt, true);
			await headers.UpdateAsync(header, true);
			FinanceReceiptEvidence.ReadSettlement(receipt, header).ShouldBeNull();
		});
		var detail = await ReceiptDetailJsonAsync(context.Owner, collection.ReceiptId);
		(detail.TryGetProperty("settlementSnapshot", out var snapshot) && snapshot.ValueKind != JsonValueKind.Null).ShouldBeFalse();
		detail.GetProperty("chargePaidAmount").GetDecimal().ShouldBe(40.25m);
	}

	private Task<JsonElement> ReceiptDetailJsonAsync(Fixture owner, Guid receiptId) => WithUnitOfWorkAsync(async () =>
	{
		var services = new DownstreamRefundServices(new FinanceServices(ServiceProvider, owner, useStoredSource: true));
		(await services.GetRequiredService<IPermissionChecker>().IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_InvoiceReceipt.View)).ShouldBeTrue();
		var service = ActivatorUtilities.CreateInstance<FeeReceiptAppService>(services);
		service.LazyServiceProvider = new AbpLazyServiceProvider(services);
		return JsonSerializer.SerializeToElement(await service.GetAsync(receiptId), new JsonSerializerOptions(JsonSerializerDefaults.Web));
	});

	private Task AssertSavedReceiptSnapshotAsync(Guid receiptId, decimal settled, decimal pending, decimal outstanding, decimal collectible) => WithUnitOfWorkAsync(async () =>
	{
		var receipt = await GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().GetAsync(receiptId);
		var metadata = await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().GetAsync(receipt.FeeCollectionTransactionId);
		var saved = FinanceReceiptEvidence.ReadSettlement(receipt, metadata);
		saved.ShouldNotBeNull();
		saved.SettledAmount.ShouldBe(settled); saved.PendingVerificationAmount.ShouldBe(pending);
		saved.FinancialOutstandingAmount.ShouldBe(outstanding); saved.CollectibleAmount.ShouldBe(collectible);
	});

	private static void AssertReceiptSnapshot(JsonElement detail, decimal settled, decimal pending, decimal outstanding, decimal collectible)
	{
		detail.TryGetProperty("settlementSnapshot", out var snapshot).ShouldBeTrue("Return saved Finance evidence instead of deriving historical receipt balances from current dues.");
		snapshot.ValueKind.ShouldBe(JsonValueKind.Object);
		detail.GetProperty("currency").GetString().ShouldBe("INR");
		snapshot.GetProperty("originalAmount").GetDecimal().ShouldBe(100.75m);
		snapshot.GetProperty("submittedAmount").GetDecimal().ShouldBe(40.25m);
		snapshot.GetProperty("settledAmount").GetDecimal().ShouldBe(settled);
		snapshot.GetProperty("pendingVerificationAmount").GetDecimal().ShouldBe(pending);
		snapshot.GetProperty("financialOutstandingAmount").GetDecimal().ShouldBe(outstanding);
		snapshot.GetProperty("collectibleAmount").GetDecimal().ShouldBe(collectible);
		snapshot.GetProperty("netWaivedAmount").GetDecimal().ShouldBe(0m);
	}
}

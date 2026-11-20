using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.CashManagement;
using ShikshaMitra.Finance.Accounts.Reconciliation;
using ShikshaMitra.Finance.Accounts.Reconciliation.Dtos;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Xunit;
using AccountTransaction = ShikshaMitra.Finance.Accounts.Incomes.Transaction;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Typed_Employee_Receipt_Should_Be_Readable_With_Its_Real_Payer()
	{
		var context = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		await ConfigureAndApproveTypedAsync(context);
		using var actor = SignIn(context.Owner);
		var form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
		var collected = await WithUnitOfWorkAsync(() => TypedService(context.Owner).CollectAsync(CashCollectionInput(context, form.Revision)));

		var detail = await WithUnitOfWorkAsync(() => GetRequiredService<IFeeReceiptAppService>().GetAsync(collected.ReceiptId));

		detail.Id.ShouldBe(collected.ReceiptId);
		detail.FeeCollectionTransactionId.ShouldBe(collected.CollectionId);
		detail.StudentId.ShouldBeNull();
		detail.FinanceFeeAccountId.ShouldBeNull();
		detail.BoardId.ShouldBeNull();
		detail.EducationMediumId.ShouldBeNull();
		var employee = detail.GetType().GetProperty("EmployeeId");
		employee.ShouldNotBeNull();
		employee.GetValue(detail).ShouldBe(context.Owner.PersonId);
		Convert.ToDecimal(detail.TotalPaidAmount).ShouldBe(40.25m);
		detail.ChargePaidAmount.ShouldBe(40.25m);
		detail.FeePaidAmount.ShouldBe(0m);
		detail.ReceiptDate.ShouldBe(new DateOnly(2026, 11, 17));
	}

	[Fact]
	public async Task Typed_Employee_Receipt_Should_Not_Be_Readable_From_Another_Tenant()
	{
		var context = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		await ConfigureAndApproveTypedAsync(context);
		using var actor = SignIn(context.Owner);
		var form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
		var collected = await WithUnitOfWorkAsync(() => TypedService(context.Owner).CollectAsync(CashCollectionInput(context, form.Revision)));
		using var otherTenant = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());

		await Should.ThrowAsync<EntityNotFoundException>(() => WithUnitOfWorkAsync(() =>
			GetRequiredService<IFeeReceiptAppService>().GetAsync(collected.ReceiptId)));
	}

	[Fact]
	public async Task Typed_Bank_Instrument_Should_Reserve_Without_Claiming_Settlement()
	{
		var (context, collected) = await CollectTypedBankInstrumentAsync();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		using var actor = SignIn(context.Owner);
		collected.ReceiptKind.ShouldBe(FeeReceiptKind.ProvisionalCharge);
		collected.VerificationStatus.ShouldBe(FeeReceiptVerificationStatus.PendingVerification);
		collected.Receivable.AcceptedPaymentAmount.ShouldBe(40.25m);
		collected.Receivable.SettledPaymentAmount.ShouldBe(0m);
		collected.Receivable.PendingVerificationAmount.ShouldBe(40.25m);
		collected.Receivable.OutstandingAmount.ShouldBe(60.50m);
		await WithUnitOfWorkAsync(async () =>
		{
			var header = await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().GetAsync(collected.CollectionId);
			header.Status.ShouldBe(FeeCollectionStatus.InReconciliation);
			(await GetRequiredService<IRepository<CashCollection, Guid>>().CountAsync()).ShouldBe(0);
			var transaction = (await GetRequiredService<IRepository<AccountTransaction, Guid>>()
				.GetListAsync(row => row.SourceId == collected.CollectionId)).Single();
			AssertRecordedPayer(transaction, FinanceExternalPayerKind.Employee, context.Owner.PersonId);
			transaction.Amount.ShouldBe(40.25m);
			transaction.IsConfirmed.ShouldBeFalse();
		});
	}

	[Fact]
	public async Task Typed_Employee_Bank_Instrument_Should_Appear_In_The_Scoped_Reconciliation_Inbox()
	{
		var (context, collected) = await CollectTypedBankInstrumentAsync();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		using var actor = SignIn(context.Reviewer);
		var inbox = await WithUnitOfWorkAsync(() => GetRequiredService<IReconciliationAppService>().GetListAsync(new()
		{
			AcademicYearId = context.Owner.YearId, Page = 1, PageSize = 20
		}));

		var row = inbox.Items.Single(item => item.Id == collected.ReceiptId);
		row.StudentId.ShouldBeNull();
		row.FinanceFeeAccountId.ShouldBeNull();
		Convert.ToDecimal(row.Amount).ShouldBe(40.25m);
		row.VerificationStatus.ShouldBe(FeeReceiptVerificationStatus.PendingVerification);
	}

	[Fact]
	public async Task Typed_Employee_Bank_Verification_Should_Link_A_Final_Receipt_Without_Another_Collection()
	{
		var (context, collected) = await CollectTypedBankInstrumentAsync();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		using var actor = SignIn(context.Reviewer);
		var action = await WithUnitOfWorkAsync(() => GetRequiredService<IReconciliationAppService>()
			.ApproveAsync(collected.ReceiptId, new() { Remarks = "Confirmed the actual receiving-bank evidence." }));

		action.Success.ShouldBeTrue();
		var form = await WithUnitOfWorkAsync(() => TypedService(context.Reviewer).GetCollectFormAsync(context.Post.Source));
		form.Receivable.AcceptedPaymentAmount.ShouldBe(40.25m);
		form.Receivable.SettledPaymentAmount.ShouldBe(40.25m);
		form.Receivable.PendingVerificationAmount.ShouldBe(0m);
		form.Receivable.OutstandingAmount.ShouldBe(60.50m);
		await WithUnitOfWorkAsync(async () =>
		{
			var receiptRepository = GetRequiredService<IRepository<FeePaymentReceipt, Guid>>();
			var original = await receiptRepository.GetAsync(collected.ReceiptId);
			original.LinkedFinalReceiptId.ShouldNotBeNull();
			original.ReceiptKind.ShouldBe(FeeReceiptKind.ProvisionalCharge);
			var final = await receiptRepository.GetAsync(original.LinkedFinalReceiptId!.Value);
			final.LinkedProvisionalReceiptId.ShouldBe(original.Id);
			final.ReceiptKind.ShouldBe(FeeReceiptKind.FinalCharge);
			final.ReceiptDate.ShouldBe(collected.SchoolReceiptDate);
			final.TotalPaidAmount.ShouldBe(40.25m);
			AssertRecordedPayer(final, FinanceExternalPayerKind.Employee, context.Owner.PersonId);
			(await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().CountAsync()).ShouldBe(1);
			(await GetRequiredService<IRepository<ChargeCollectionRecord, Guid>>().CountAsync()).ShouldBe(1);
			var income = (await GetRequiredService<IRepository<AccountTransaction, Guid>>()
				.GetListAsync(row => row.SourceId == collected.CollectionId)).Single();
			income.IsConfirmed.ShouldBeTrue();
		});
	}

	[Fact]
	public async Task Typed_Employee_Bank_Rejection_Should_Release_Only_The_Original_Reservation()
	{
		var (context, collected) = await CollectTypedBankInstrumentAsync();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		using var actor = SignIn(context.Reviewer);
		var action = await WithUnitOfWorkAsync(() => GetRequiredService<IReconciliationAppService>()
			.RejectAsync(collected.ReceiptId, new() { Remarks = "Bank confirmed that the instrument was not credited." }));

		action.Success.ShouldBeTrue();
		var form = await WithUnitOfWorkAsync(() => TypedService(context.Reviewer).GetCollectFormAsync(context.Post.Source));
		form.Receivable.AcceptedPaymentAmount.ShouldBe(0m);
		form.Receivable.SettledPaymentAmount.ShouldBe(0m);
		form.Receivable.PendingVerificationAmount.ShouldBe(0m);
		form.Receivable.OutstandingAmount.ShouldBe(100.75m);
		await WithUnitOfWorkAsync(async () =>
		{
			var header = await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().GetAsync(collected.CollectionId);
			header.Status.ShouldBe(FeeCollectionStatus.Reversed);
			var original = await GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().GetAsync(collected.ReceiptId);
			original.VerificationStatus.ShouldBe(FeeReceiptVerificationStatus.Rejected);
			original.LinkedFinalReceiptId.ShouldBeNull();
			(await GetRequiredService<IRepository<ChargeCollectionRecord, Guid>>().CountAsync()).ShouldBe(1);
			(await GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().CountAsync()).ShouldBe(1);
		});
	}

	private async Task<(TypedSettlement Context, FinanceExternalCollectionDto Collection)> CollectTypedBankInstrumentAsync()
	{
		var context = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		await ConfigureAndApproveTypedAsync(context);
		var bank = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<BankAccount, Guid>>().InsertAsync(new BankAccount(
			Guid.NewGuid(), context.Owner.YearId, "School Receiving Bank", "School branch", null,
			"Configured School", BankAccountType.Current, "1234567890", "Library recovery collection", "TEST0000001", null,
			tenantId: context.Owner.TenantId), autoSave: true));
		await WithUnitOfWorkAsync(async () =>
		{
			var reviewer = await GetRequiredService<IIdentityUserRepository>().GetAsync(context.Reviewer.ActorId, includeDetails: true);
			var roleIds = reviewer.Roles.Select(role => role.RoleId).ToArray();
			var roles = await GetRequiredService<IRepository<IdentityRole, Guid>>().GetListAsync(role =>
				role.TenantId == context.Owner.TenantId && Enumerable.Contains(roleIds, role.Id));
			roles.ShouldNotBeEmpty();
			foreach (var role in roles)
				foreach (var permission in new[] { ShikshaMitraSubmodulePermissions.Finance.Accounts_Reconciliation.View,
					ShikshaMitraSubmodulePermissions.Finance.Accounts_Reconciliation.Add })
					await GetRequiredService<IPermissionManager>().SetAsync(permission, "R", role.Name, true);
		});
		using var actor = SignIn(context.Owner);
		var form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
		var collected = await WithUnitOfWorkAsync(() => TypedService(context.Owner).CollectAsync(new()
		{
			Source = context.Post.Source, Revision = form.Revision, IdempotencyKey = Guid.NewGuid().ToString("N"),
			SchoolReceiptDate = new DateOnly(2026, 11, 17), Payments = [new()
			{
				PaymentMethod = PaymentMethodEnum.UPI, Amount = 40.25m, BankAccountId = bank.Id,
				Details = new() { ["upiTransactionId"] = "ACTUAL-UPI-84", ["upiId"] = "school@bank" }
			}]
		}));
		return (context, collected);
	}
}

using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Accounts.CashManagement;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Xunit;
using AccountTransaction = ShikshaMitra.Finance.Accounts.Incomes.Transaction;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Typed_Settlement_Should_Require_Explicit_Source_Policy_Configuration()
	{
		var context = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Student);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		using var actor = SignIn(context.Owner);

		var form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));

		form.SettlementPolicyBindingId.ShouldBeNull();
		form.CollectionPolicyVersionId.ShouldBeNull();
		form.CanCollect.ShouldBeFalse();
		form.SetupIssues.ShouldContain(issue => issue.Contains("policy", StringComparison.OrdinalIgnoreCase));
		await AssertNoTypedCollectionsAsync();
	}

	[Fact]
	public async Task Typed_Settlement_Policy_Binding_Should_Preserve_Original_Post_Facts_And_Replay()
	{
		var context = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Student);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		using var actor = SignIn(context.Owner);
		var form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
		var input = new BindFinanceExternalSettlementPolicyInput
		{
			Source = context.Post.Source, Revision = form.Revision, CollectionPolicyVersionId = context.CollectionPolicyVersionId,
			Reason = "Confirmed the school's configured recovery policy.", IdempotencyKey = Guid.NewGuid().ToString("N")
		};

		var bound = await WithUnitOfWorkAsync(() => TypedService(context.Owner).BindPolicyAsync(input));
		var replay = await WithUnitOfWorkAsync(() => TypedService(context.Owner).BindPolicyAsync(input));
		var originalPost = await WithUnitOfWorkAsync(() => Service(context.Owner, useStoredSource: true).PostAsync(context.Post));

		bound.SettlementPolicyBindingId.ShouldNotBeNull();
		replay.SettlementPolicyBindingId.ShouldBe(bound.SettlementPolicyBindingId);
		bound.CollectionPolicyVersionId.ShouldBe(context.CollectionPolicyVersionId);
		originalPost.FinanceChargeId.ShouldBe(context.Posted.FinanceChargeId);
		await WithUnitOfWorkAsync(async () =>
		{
			var link = (await GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>()
				.GetListAsync(row => row.SourceId == context.Owner.AssessmentId)).Single();
			link.CollectionPolicyVersionId.ShouldBeNull("The original Post did not choose a policy.");
			link.SourceVersion.ShouldBe(context.Owner.Version);
		});
		await AssertNoTypedCollectionsAsync();
	}

	[Theory]
	[InlineData(FinanceExternalPayerKind.Student)]
	[InlineData(FinanceExternalPayerKind.Employee)]
	public async Task Typed_Settlement_Should_Collect_A_Partial_Decimal_Payment_For_A_Departed_Real_Payer(
		FinanceExternalPayerKind kind)
	{
		var context = await SeedTypedSettlementAsync(kind);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		await ConfigureAndApproveTypedAsync(context);
		using var actor = SignIn(context.Owner);
		var form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
		form.CanCollect.ShouldBeTrue();
		var input = CashCollectionInput(context, form.Revision);

		var collected = await WithUnitOfWorkAsync(() => TypedService(context.Owner).CollectAsync(input));
		var replay = await WithUnitOfWorkAsync(() => TypedService(context.Owner).CollectAsync(input));

		replay.CollectionId.ShouldBe(collected.CollectionId);
		replay.ReceiptId.ShouldBe(collected.ReceiptId);
		collected.AcceptedAmount.ShouldBe(40.25m);
		collected.Receivable.OriginalAmount.ShouldBe(100.75m);
		collected.Receivable.AcceptedPaymentAmount.ShouldBe(40.25m);
		collected.Receivable.SettledPaymentAmount.ShouldBe(40.25m);
		collected.Receivable.PendingVerificationAmount.ShouldBe(0m);
		collected.Receivable.OutstandingAmount.ShouldBe(60.50m);
		collected.ReceiptKind.ShouldBe(FeeReceiptKind.FinalCharge);
		collected.SchoolReceiptDate.ShouldBe(new DateOnly(2026, 11, 17));
		await WithUnitOfWorkAsync(async () =>
		{
			var metadata = await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().GetAsync(collected.CollectionId);
			var allocation = (await GetRequiredService<IRepository<ChargeCollectionRecord, Guid>>()
				.GetListAsync(row => row.FeeRecordMetadataId == metadata.Id)).Single();
			var instrument = (await GetRequiredService<IRepository<PaymentMethodRecord, Guid>>()
				.GetListAsync(row => row.FeeRecordMetadataId == metadata.Id)).Single();
			var receipt = await GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().GetAsync(collected.ReceiptId);
			var cash = (await GetRequiredService<IRepository<CashCollection, Guid>>()
				.GetListAsync(row => row.SourceId == metadata.Id)).Single();
			var income = (await GetRequiredService<IRepository<AccountTransaction, Guid>>()
				.GetListAsync(row => row.SourceId == metadata.Id)).Single();
			foreach (var row in new object[] { metadata, allocation, instrument, receipt, cash, income })
				AssertRecordedPayer(row, kind, context.Owner.PersonId);
			Convert.ToDecimal(metadata.TotalPaidFee).ShouldBe(40.25m);
			Convert.ToDecimal(allocation.Amount).ShouldBe(40.25m);
			instrument.Amount.ShouldBe(40.25m);
			Convert.ToDecimal(receipt.TotalPaidAmount).ShouldBe(40.25m);
			receipt.FeeCollectionTransactionId.ShouldBe(metadata.Id);
			receipt.ReceiptNumber.ShouldBe(collected.ReceiptNumber);
			receipt.ReceiptDate.ShouldBe(collected.SchoolReceiptDate);
			cash.Amount.ShouldBe(40.25m);
			income.Amount.ShouldBe(40.25m);
			income.IsConfirmed.ShouldBeFalse("Collected cash still requires the existing cash handover approval.");
			(await GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().CountAsync()).ShouldBe(1);
			(await GetRequiredService<IRepository<StudentFeeStructure, Guid>>().CountAsync()).ShouldBe(0);
			(await GetRequiredService<IRepository<FinanceFeeAccount, Guid>>().CountAsync()).ShouldBe(0);
		});
	}

	[Fact]
	public async Task Typed_Settlement_Should_Not_Let_The_Assessment_Maker_Approve_Their_Own_Charge()
	{
		var context = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Student);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		using var actor = SignIn(context.Owner);
		var form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));

		await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => TypedService(context.Owner).ApproveAsync(new()
		{
			Source = context.Post.Source, Revision = form.Revision,
			Reason = "Attempted self approval", IdempotencyKey = Guid.NewGuid().ToString("N")
		})));
		await AssertNoTypedCollectionsAsync();
	}

	[Fact]
	public async Task Typed_Settlement_Should_Not_Collect_An_Unapproved_Assessment()
	{
		var context = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Student);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		using var actor = SignIn(context.Owner);
		var form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
		form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).BindPolicyAsync(new()
		{
			Source = context.Post.Source, Revision = form.Revision, CollectionPolicyVersionId = context.CollectionPolicyVersionId,
			Reason = "Confirmed the school's configured recovery policy.", IdempotencyKey = Guid.NewGuid().ToString("N")
		}));

		await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
			TypedService(context.Owner).CollectAsync(CashCollectionInput(context, form.Revision))));
		await AssertNoTypedCollectionsAsync();
	}

	[Fact]
	public async Task Typed_Settlement_Should_Reject_A_Stale_Balance_Revision()
	{
		var context = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Student);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		await ConfigureAndApproveTypedAsync(context);
		using var actor = SignIn(context.Owner);
		var form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
		await WithUnitOfWorkAsync(() => TypedService(context.Owner).CollectAsync(CashCollectionInput(context, form.Revision)));

		await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
			TypedService(context.Owner).CollectAsync(CashCollectionInput(context, form.Revision))));
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().CountAsync()).ShouldBe(1));
	}

	[Fact]
	public async Task Typed_Settlement_Should_Recheck_A_Removed_Collection_Action_Grant()
	{
		var context = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		await ConfigureAndApproveTypedAsync(context);
		using var actor = SignIn(context.Owner);
		var form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
		await WithUnitOfWorkAsync(() => GetRequiredService<IPermissionManager>().SetAsync(
			ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add,
			"R", $"external-finance-{context.Owner.ActorId:N}", false));

		await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() =>
			TypedService(context.Owner).CollectAsync(CashCollectionInput(context, form.Revision))));
		await AssertNoTypedCollectionsAsync();
	}

	[Fact]
	public async Task Typed_Settlement_Should_Apply_And_Reverse_Only_The_Requested_Part_Of_A_Waiver()
	{
		var context = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		await ConfigureAndApproveTypedAsync(context);
		using var actor = SignIn(context.Owner);
		var form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
		var applied = await WithUnitOfWorkAsync(() => TypedService(context.Owner).RequestWaiverAsync(new()
		{
			Source = context.Post.Source, Revision = form.Revision, Amount = 10.10m,
			Reason = "Approved partial school assistance.", IdempotencyKey = Guid.NewGuid().ToString("N")
		}));
		applied.State.ShouldBe(FinanceExternalWaiverState.Applied);
		applied.AppliedAmount.ShouldBe(10.10m);
		applied.Receivable.OutstandingAmount.ShouldBe(90.65m);
		form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));

		var reversed = await WithUnitOfWorkAsync(() => TypedService(context.Owner).ReverseWaiverAsync(new()
		{
			Source = context.Post.Source, Revision = form.Revision, WaiverApplicationId = applied.ApplicationId!.Value,
			Amount = 2.05m, Reason = "Corrected the approved assistance amount.", IdempotencyKey = Guid.NewGuid().ToString("N")
		}));

		reversed.AppliedAmount.ShouldBe(10.10m);
		reversed.ReversedAmount.ShouldBe(2.05m);
		reversed.Receivable.WaivedAmount.ShouldBe(8.05m);
		reversed.Receivable.OutstandingAmount.ShouldBe(92.70m);
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<ChargeWaiverApplication, Guid>>().GetAsync(applied.ApplicationId.Value))
				.EffectiveAmount.ShouldBe(10.10m));
	}

	[Fact]
	public async Task Typed_Settlement_Should_Keep_A_Policy_Controlled_Waiver_Pending_Until_A_Different_Approver_Decides()
	{
		var context = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Student, requiresWaiverApproval: true);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		await ConfigureAndApproveTypedAsync(context);
		FinanceExternalWaiverDto pending;
		using (SignIn(context.Owner))
		{
			var form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
			pending = await WithUnitOfWorkAsync(() => TypedService(context.Owner).RequestWaiverAsync(new()
			{
				Source = context.Post.Source, Revision = form.Revision, Amount = 10.10m,
				Reason = "Requested school assistance.", IdempotencyKey = Guid.NewGuid().ToString("N")
			}));
			pending.State.ShouldBe(FinanceExternalWaiverState.PendingApproval);
			pending.ApplicationId.ShouldBeNull();
			pending.Receivable.WaivedAmount.ShouldBe(0m);
			pending.Receivable.OutstandingAmount.ShouldBe(100.75m);
			form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
			await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => TypedService(context.Owner).ApproveWaiverAsync(new()
			{
				Source = context.Post.Source, Revision = form.Revision, WaiverRequestId = pending.RequestId,
				Reason = "Attempted self approval", IdempotencyKey = Guid.NewGuid().ToString("N")
			})));
		}
		using (SignIn(context.Reviewer))
		{
			var form = await WithUnitOfWorkAsync(() => TypedService(context.Reviewer).GetCollectFormAsync(context.Post.Source));
			var approved = await WithUnitOfWorkAsync(() => TypedService(context.Reviewer).ApproveWaiverAsync(new()
			{
				Source = context.Post.Source, Revision = form.Revision, WaiverRequestId = pending.RequestId,
				Reason = "Reviewed the school's assistance evidence.", IdempotencyKey = Guid.NewGuid().ToString("N")
			}));
			approved.State.ShouldBe(FinanceExternalWaiverState.Applied);
			approved.Receivable.OutstandingAmount.ShouldBe(90.65m);
		}
	}

	private static FinanceExternalCollectInput CashCollectionInput(TypedSettlement context, string revision) => new()
	{
		Source = context.Post.Source, Revision = revision, IdempotencyKey = Guid.NewGuid().ToString("N"),
		SchoolReceiptDate = new DateOnly(2026, 11, 17),
		Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 40.25m }]
	};

	private Task AssertNoTypedCollectionsAsync() => WithUnitOfWorkAsync(async () =>
	{
		(await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().CountAsync()).ShouldBe(0);
		(await GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().CountAsync()).ShouldBe(0);
	});

	private static void AssertRecordedPayer(object row, FinanceExternalPayerKind kind, Guid personId)
	{
		var student = row.GetType().GetProperty("StudentId");
		var employee = row.GetType().GetProperty("EmployeeId");
		student.ShouldNotBeNull();
		employee.ShouldNotBeNull("Every persisted settlement row must retain its real payer identity.");
		object? expectedStudent = kind == FinanceExternalPayerKind.Student ? personId : null;
		object? expectedEmployee = kind == FinanceExternalPayerKind.Employee ? personId : null;
		student.GetValue(row).ShouldBe(expectedStudent);
		employee.GetValue(row).ShouldBe(expectedEmployee);
	}
}

using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Canonical_Consumer_Position_Should_Keep_Partial_Payment_And_Net_Waiver_Separate()
	{
		var fixture = await SeedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		var owner = fixture.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var form = await WithUnitOfWorkAsync(() => TypedService(owner).GetCollectFormAsync(fixture.Settlement.Post.Source));
		var applied = await WithUnitOfWorkAsync(() => TypedService(owner).RequestWaiverAsync(new()
		{
			Source = fixture.Settlement.Post.Source, Revision = form.Revision, Amount = 10.10m,
			Reason = "Reviewed partial assistance against the original source.", IdempotencyKey = Guid.NewGuid().ToString("N")
		}));
		applied.AppliedAmount.ShouldBe(10.10m);
		var applicationId = applied.ApplicationId ?? throw new InvalidOperationException("The actual applied waiver must have an application ID.");
		form = await WithUnitOfWorkAsync(() => TypedService(owner).GetCollectFormAsync(fixture.Settlement.Post.Source));
		await WithUnitOfWorkAsync(() => TypedService(owner).ReverseWaiverAsync(new()
		{
			Source = fixture.Settlement.Post.Source, Revision = form.Revision, WaiverApplicationId = applicationId,
			Amount = 4.05m, Reason = "Corrected the independently recorded assistance amount.", IdempotencyKey = Guid.NewGuid().ToString("N")
		}));
		var row = await ConsumerPositionAsync(owner);
		row.Evidence.ShouldBe(FinanceExternalPositionEvidence.Verified);
		row.Original.ShouldBe(100.75m); row.Accepted.ShouldBe(40.25m); row.Settled.ShouldBe(40.25m);
		row.Pending.ShouldBe(0m); row.NetWaived.ShouldBe(6.05m); row.FinancialOutstanding.ShouldBe(54.45m);
		row.Collectible.ShouldBe(54.45m); row.Refunded.ShouldBe(0m);
		await AssertConsumerDetailParityAsync(owner, row);
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<ChargeWaiverApplication, Guid>>().GetAsync(applicationId))
				.EffectiveAmount.ShouldBe(10.10m);
			(await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().GetAsync(fixture.Collection.CollectionId)).TotalPaidFee.ShouldBe(40.25m);
		});
	}

	[Fact]
	public async Task Canonical_Consumer_Position_Should_Keep_Fully_Reserved_Bank_Payment_Unsettled()
	{
		var (context, first) = await CollectTypedBankInstrumentAsync();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		using var actor = SignIn(context.Owner);
		var bankId = await WithUnitOfWorkAsync(async () => (await GetRequiredService<IRepository<BankAccount, Guid>>()
			.GetListAsync(row => row.TenantId == context.Owner.TenantId)).Single().Id);
		var form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
		first.Receivable.PendingVerificationAmount.ShouldBe(40.25m);
		var final = await WithUnitOfWorkAsync(() => TypedService(context.Owner).CollectAsync(new()
		{
			Source = context.Post.Source, Revision = form.Revision, IdempotencyKey = Guid.NewGuid().ToString("N"),
			SchoolReceiptDate = new(2026, 11, 17), Payments = [new()
			{ PaymentMethod = PaymentMethodEnum.UPI, Amount = 60.50m, BankAccountId = bankId,
				Details = new() { ["upiTransactionId"] = "SECOND-RESERVED-UPI", ["upiId"] = "school@bank" } }]
		}));
		final.Receivable.PendingVerificationAmount.ShouldBe(100.75m);
		var row = await ConsumerPositionAsync(context.Owner);
		row.Accepted.ShouldBe(100.75m); row.Settled.ShouldBe(0m); row.Pending.ShouldBe(100.75m);
		row.FinancialOutstanding.ShouldBe(100.75m); row.Collectible.ShouldBe(0m);
		await AssertConsumerDetailParityAsync(context.Owner, row);
	}

	[Fact]
	public async Task Canonical_Consumer_Position_Should_Not_Make_Pending_Or_Rejected_Assessments_Payable()
	{
		var context = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Student);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		using var actor = SignIn(context.Owner);
		var pending = await ConsumerPositionAsync(context.Owner);
		pending.ApprovalStatus.ShouldBe(ChargeStatus.Pending); pending.Original.ShouldBe(100.75m);
		pending.FinancialOutstanding.ShouldBe(0m); pending.Collectible.ShouldBe(0m);
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<Charge, Guid>>();
			var charge = await repository.GetAsync(context.Posted.FinanceChargeId);
			charge.Reject(context.Reviewer.ActorId, "Independent reviewer rejected the posted assessment.");
			await repository.UpdateAsync(charge, true);
		});
		var rejected = await ConsumerPositionAsync(context.Owner);
		rejected.ApprovalStatus.ShouldBe(ChargeStatus.Rejected); rejected.Original.ShouldBe(100.75m);
		rejected.FinancialOutstanding.ShouldBe(0m); rejected.Collectible.ShouldBe(0m);
		await AssertConsumerDetailParityAsync(context.Owner, rejected);
	}
}

using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Collections;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Member_Charge_Position_Should_Keep_Actual_Cash_And_Net_Waiver_Separate_Under_Members_View()
	{
		AssertNullableMemberChargePositionDefault();
		var fixture = await SeedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		var context = fixture.Settlement;
		var owner = context.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var form = await WithUnitOfWorkAsync(() => TypedService(owner).GetCollectFormAsync(context.Post.Source));
		var waiver = await WithUnitOfWorkAsync(() => TypedService(owner).RequestWaiverAsync(new()
		{
			Source = context.Post.Source, Revision = form.Revision, Amount = 10.10m,
			Reason = "Actual reviewed assistance for member history.", IdempotencyKey = Guid.NewGuid().ToString("N")
		}));
		var applicationId = waiver.ApplicationId ?? throw new InvalidOperationException("Expected an actual waiver application.");
		form = await WithUnitOfWorkAsync(() => TypedService(owner).GetCollectFormAsync(context.Post.Source));
		await WithUnitOfWorkAsync(() => TypedService(owner).ReverseWaiverAsync(new()
		{
			Source = context.Post.Source, Revision = form.Revision, WaiverApplicationId = applicationId,
			Amount = 4.05m, Reason = "Actual recorded partial reversal.", IdempotencyKey = Guid.NewGuid().ToString("N")
		}));
		var retained = await CaptureDashboardClockSourceAsync(owner);
		var denied = await RestrictMemberChargeReadAsync(owner);
		await AssertMemberChargeReadAuthorityAsync(owner, denied);
		await AssertMemberChargeTargetAsync(owner);
		var actual = await MemberChargePositionAsync(owner);
		actual.Evidence.ShouldBe(FinanceExternalPositionEvidence.Verified);
		actual.Settled.ShouldBe(40.25m); actual.NetWaived.ShouldBe(6.05m); actual.Collectible.ShouldBe(54.45m);

		var page = await WithUnitOfWorkAsync(() => MemberChargeReadService(owner).GetMemberChargesAsync(owner.PersonId));

		page.TotalCount.ShouldBe(1);
		var item = page.Items.ShouldHaveSingleItem();
		item.Id.ShouldBe(owner.AssessmentId); item.Amount.ShouldBe(100.75m); item.PaymentStatus.ShouldBe((int)ChargePaymentStatus.Pending);
		item.SchoolChargeDate.ShouldBe(owner.AssessmentDate); item.SchoolDueDate.ShouldBe(owner.DueDate);
		MemberChargeWire(item).GetProperty("amountText").GetString().ShouldBe("100.75");
		var position = RequireMemberChargePosition(item);
		position.GetProperty("evidence").GetInt32().ShouldBe((int)FinanceExternalPositionEvidence.Verified);
		position.GetProperty("currency").GetString().ShouldBe("INR");
		var amounts = position.GetProperty("amounts");
		amounts.GetProperty("originalAmountText").GetString().ShouldBe("100.75");
		amounts.GetProperty("acceptedAmountText").GetString().ShouldBe("40.25");
		amounts.GetProperty("settledAmountText").GetString().ShouldBe("40.25");
		amounts.GetProperty("pendingVerificationAmountText").GetString().ShouldBe("0.00");
		amounts.GetProperty("netWaivedAmountText").GetString().ShouldBe("6.05");
		amounts.GetProperty("financialOutstandingAmountText").GetString().ShouldBe("54.45");
		amounts.GetProperty("collectibleAmountText").GetString().ShouldBe("54.45");
		amounts.GetProperty("refundedAmountText").GetString().ShouldBe("0.00");
		(await CaptureDashboardClockSourceAsync(owner)).ShouldBe(retained);
	}

	[Fact]
	public async Task Member_Charge_Position_Should_Keep_Fully_Reserved_Bank_Money_Unsettled()
	{
		var (context, first) = await CollectTypedBankInstrumentAsync();
		var owner = context.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		first.Receivable.PendingVerificationAmount.ShouldBe(40.25m);
		var bankId = await WithUnitOfWorkAsync(async () => (await GetRequiredService<IRepository<BankAccount, Guid>>()
			.GetListAsync(row => row.TenantId == owner.TenantId)).ShouldHaveSingleItem().Id);
		var form = await WithUnitOfWorkAsync(() => TypedService(owner).GetCollectFormAsync(context.Post.Source));
		var reserved = await WithUnitOfWorkAsync(() => TypedService(owner).CollectAsync(new()
		{
			Source = context.Post.Source, Revision = form.Revision, IdempotencyKey = Guid.NewGuid().ToString("N"),
			SchoolReceiptDate = new(2026, 11, 17), Payments = [new()
			{
				PaymentMethod = PaymentMethodEnum.UPI, Amount = 60.50m, BankAccountId = bankId,
				Details = new() { ["upiTransactionId"] = "MEMBER-SECOND-RESERVED-UPI", ["upiId"] = "school@bank" }
			}]
		}));
		reserved.Receivable.PendingVerificationAmount.ShouldBe(100.75m);
		reserved.Receivable.OutstandingAmount.ShouldBe(0m);
		var retained = await CaptureDashboardClockSourceAsync(owner);
		var denied = await RestrictMemberChargeReadAsync(owner);
		await AssertMemberChargeReadAuthorityAsync(owner, denied);
		await AssertMemberChargeTargetAsync(owner);
		var actual = await MemberChargePositionAsync(owner);
		actual.Evidence.ShouldBe(FinanceExternalPositionEvidence.Verified);
		actual.Settled.ShouldBe(0m); actual.Pending.ShouldBe(100.75m);
		actual.FinancialOutstanding.ShouldBe(100.75m); actual.Collectible.ShouldBe(0m);

		var page = await WithUnitOfWorkAsync(() => MemberChargeReadService(owner).GetMemberChargesAsync(owner.PersonId));

		page.TotalCount.ShouldBe(1);
		var item = page.Items.ShouldHaveSingleItem();
		item.Amount.ShouldBe(100.75m); item.PaymentStatus.ShouldBe((int)ChargePaymentStatus.Pending);
		var amounts = RequireMemberChargePosition(item).GetProperty("amounts");
		amounts.GetProperty("settledAmountText").GetString().ShouldBe("0.00");
		amounts.GetProperty("pendingVerificationAmountText").GetString().ShouldBe("100.75");
		amounts.GetProperty("financialOutstandingAmountText").GetString().ShouldBe("100.75");
		amounts.GetProperty("collectibleAmountText").GetString().ShouldBe("0.00");
		(await CaptureDashboardClockSourceAsync(owner)).ShouldBe(retained);
	}
}

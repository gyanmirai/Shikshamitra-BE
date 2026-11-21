using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Library.Policies;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

// Outside the active graph: actual payments precede Dashboard-only authority.
public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Dashboard_Collection_Alert_Should_Not_Ask_For_An_Actually_Settled_Assessment()
	{
		var fixture = await SeedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		var context = fixture.Settlement;
		var owner = context.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var form = await WithUnitOfWorkAsync(() => TypedService(owner).GetCollectFormAsync(context.Post.Source));
		var remainder = CashCollectionInput(context, form.Revision);
		remainder.Payments.Single().Amount = 60.50m;
		var paid = await WithUnitOfWorkAsync(() => TypedService(owner).CollectAsync(remainder));
		paid.Receivable.SettledPaymentAmount.ShouldBe(100.75m);
		paid.Receivable.PendingVerificationAmount.ShouldBe(0m);
		paid.Receivable.FinancialOutstandingAmount.ShouldBe(0m);
		paid.Receivable.OutstandingAmount.ShouldBe(0m);
		var before = await CaptureDashboardClockSourceAsync(owner);
		await SetDashboardClockZoneAsync(owner, LibraryPolicySettingNames.CalendarTimeZone, "UTC");
		var instant = owner.DueDate.AddDays(1).ToDateTime(new TimeOnly(0, 30), DateTimeKind.Utc);
		var denied = await RestrictConsumerToDashboardAsync(owner);
		await AssertDashboardConsumerAuthorityAsync(owner, denied);
		var position = await DashboardAlertPositionAsync(owner, owner.YearId);
		position.Evidence.ShouldBe(FinanceExternalPositionEvidence.Verified);
		position.Settled.ShouldBe(100.75m); position.Collectible.ShouldBe(0m);

		var overview = await WithUnitOfWorkAsync(() => ConsumerDashboardAtUtc(owner, instant).GetOverviewAsync(owner.YearId));

		overview.SchoolAsOfDate.ShouldBe(owner.DueDate.AddDays(1)); overview.SchoolDateSource.ShouldBe("ConfirmedLibraryClock");
		overview.Charges.PendingCount.ShouldBe(1); overview.Charges.PendingAmount.ShouldBe(100.75m);
		overview.Charges.PaidCount.ShouldBe(0);
		var total = overview.FinancePosition.CurrencyTotals.ShouldHaveSingleItem();
		total.SettledAmountText.ShouldBe("100.75"); total.CollectibleAmountText.ShouldBe("0.00");
		AssertNoDashboardCollectionAlerts(overview);
		overview.Alerts.ShouldNotContain(alert => alert.Type == "unpostedCharges" || alert.Type == "reviewCharges");
		(await CaptureDashboardClockSourceAsync(owner)).ShouldBe(before);
		(await DashboardAlertPositionAsync(owner, owner.YearId)).Settled.ShouldBe(100.75m);
	}

	[Fact]
	public async Task Dashboard_Collection_Alert_Should_Not_Ask_For_Money_Already_Reserved_For_Bank_Verification()
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
				Details = new() { ["upiTransactionId"] = "DASHBOARD-ALERT-SECOND-UPI", ["upiId"] = "school@bank" }
			}]
		}));
		reserved.Receivable.PendingVerificationAmount.ShouldBe(100.75m);
		reserved.Receivable.SettledPaymentAmount.ShouldBe(0m);
		reserved.Receivable.FinancialOutstandingAmount.ShouldBe(100.75m);
		reserved.Receivable.OutstandingAmount.ShouldBe(0m);
		var before = await CaptureDashboardClockSourceAsync(owner);
		await SetDashboardClockZoneAsync(owner, LibraryPolicySettingNames.CalendarTimeZone, "UTC");
		var instant = owner.DueDate.AddDays(1).ToDateTime(new TimeOnly(0, 30), DateTimeKind.Utc);
		var denied = await RestrictConsumerToDashboardAsync(owner);
		await AssertDashboardConsumerAuthorityAsync(owner, denied);
		var position = await DashboardAlertPositionAsync(owner, owner.YearId);
		position.Evidence.ShouldBe(FinanceExternalPositionEvidence.Verified);
		position.Pending.ShouldBe(100.75m); position.Settled.ShouldBe(0m);
		position.FinancialOutstanding.ShouldBe(100.75m); position.Collectible.ShouldBe(0m);

		var overview = await WithUnitOfWorkAsync(() => ConsumerDashboardAtUtc(owner, instant).GetOverviewAsync(owner.YearId));

		overview.SchoolAsOfDate.ShouldBe(owner.DueDate.AddDays(1)); overview.SchoolDateSource.ShouldBe("ConfirmedLibraryClock");
		overview.Charges.PendingCount.ShouldBe(1); overview.Charges.PendingAmount.ShouldBe(100.75m);
		var total = overview.FinancePosition.CurrencyTotals.ShouldHaveSingleItem();
		total.PendingVerificationAmountText.ShouldBe("100.75"); total.FinancialOutstandingAmountText.ShouldBe("100.75");
		total.SettledAmountText.ShouldBe("0.00"); total.CollectibleAmountText.ShouldBe("0.00");
		AssertNoDashboardCollectionAlerts(overview);
		overview.Alerts.ShouldNotContain(alert => alert.Type == "unpostedCharges" || alert.Type == "reviewCharges");
		(await CaptureDashboardClockSourceAsync(owner)).ShouldBe(before);
		(await DashboardAlertPositionAsync(owner, owner.YearId)).Pending.ShouldBe(100.75m);
	}
}

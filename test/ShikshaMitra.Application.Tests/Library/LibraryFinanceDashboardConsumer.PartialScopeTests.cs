using System;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Charges;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

// Uses the shared real Dashboard service fixture; no fabricated source balances.
public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Dashboard_Finance_Position_Should_Separate_Actual_Cash_And_Net_Waiver_After_Reversal()
	{
		var fixture = await SeedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		var context = fixture.Settlement;
		var owner = context.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var form = await WithUnitOfWorkAsync(() => TypedService(owner).GetCollectFormAsync(context.Post.Source));
		var applied = await WithUnitOfWorkAsync(() => TypedService(owner).RequestWaiverAsync(new()
		{
			Source = context.Post.Source, Revision = form.Revision, Amount = 10.10m,
			Reason = "Actual partial assistance before Dashboard read.", IdempotencyKey = Guid.NewGuid().ToString("N")
		}));
		applied.AppliedAmount.ShouldBe(10.10m);
		var application = applied.ApplicationId ?? throw new InvalidOperationException("Expected a real waiver application.");
		form = await WithUnitOfWorkAsync(() => TypedService(owner).GetCollectFormAsync(context.Post.Source));
		await WithUnitOfWorkAsync(() => TypedService(owner).ReverseWaiverAsync(new()
		{
			Source = context.Post.Source, Revision = form.Revision, WaiverApplicationId = application,
			Amount = 4.05m, Reason = "Recorded partial reversal.", IdempotencyKey = Guid.NewGuid().ToString("N")
		}));
		var denied = await RestrictConsumerToDashboardAsync(owner);
		await AssertDashboardConsumerAuthorityAsync(owner, denied);
		await WithUnitOfWorkAsync(async () =>
		{
			var query = await ConsumerPositionService(owner).GetQueryAsync(new(LibraryChargePositionReadPurpose.Dashboard, owner.YearId));
			var actual = await GetRequiredService<Volo.Abp.Linq.IAsyncQueryableExecuter>().SingleAsync(query);
			actual.NetWaived.ShouldBe(6.05m); actual.Collectible.ShouldBe(54.45m);
		});
		var overview = await WithUnitOfWorkAsync(() => ConsumerDashboardService(owner).GetOverviewAsync(owner.YearId));
		// Compatibility fields retain assessment principal and recorded local status.
		overview.Charges.PendingCount.ShouldBe(1); overview.Charges.PendingAmount.ShouldBe(100.75m);
		overview.Charges.PaidCount.ShouldBe(0); overview.Charges.WaivedCount.ShouldBe(0);
		var summary = DashboardFinanceSummary(overview);
		summary.GetProperty("verifiedCount").GetInt32().ShouldBe(1);
		var totals = summary.GetProperty("currencyTotals").EnumerateArray().ShouldHaveSingleItem();
		totals.GetProperty("currency").GetString().ShouldBe("INR");
		totals.GetProperty("originalAmountText").GetString().ShouldBe("100.75");
		totals.GetProperty("acceptedAmountText").GetString().ShouldBe("40.25");
		totals.GetProperty("settledAmountText").GetString().ShouldBe("40.25");
		totals.GetProperty("pendingVerificationAmountText").GetString().ShouldBe("0.00");
		totals.GetProperty("netWaivedAmountText").GetString().ShouldBe("6.05");
		totals.GetProperty("financialOutstandingAmountText").GetString().ShouldBe("54.45");
		totals.GetProperty("collectibleAmountText").GetString().ShouldBe("54.45");
		totals.GetProperty("refundedAmountText").GetString().ShouldBe("0.00");
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Dashboard_Finance_And_Compatibility_Amounts_Should_Use_Recorded_Source_Scope(bool currentPlacement)
	{
		var moved = await SeedMovedTypedCashAsync();
		var owner = moved.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var denied = await RestrictConsumerToDashboardAsync(owner);
		await SetMovedCashReaderScopeAsync(moved, currentPlacement);
		await AssertConsumerDashboardAuthorityAsync(moved, denied, currentPlacement);
		var overview = await WithUnitOfWorkAsync(() => ConsumerDashboardService(owner).GetOverviewAsync(owner.YearId));
		// This assertion precedes the missing additive wire property: it witnesses
		// the current-member compatibility population bug independently.
		overview.Charges.PendingCount.ShouldBe(currentPlacement ? 0 : 1);
		overview.Charges.PendingAmount.ShouldBe(currentPlacement ? 0m : 100.75m);
		var summary = DashboardFinanceSummary(overview);
		summary.GetProperty("verifiedCount").GetInt32().ShouldBe(currentPlacement ? 0 : 1);
		summary.GetProperty("needsReviewCount").GetInt32().ShouldBe(0);
		summary.GetProperty("unpostedCount").GetInt32().ShouldBe(0);
		var groups = summary.GetProperty("currencyTotals").EnumerateArray();
		if (currentPlacement) groups.ShouldBeEmpty();
		else groups.ShouldHaveSingleItem().GetProperty("settledAmountText").GetString().ShouldBe("40.25");
	}
}

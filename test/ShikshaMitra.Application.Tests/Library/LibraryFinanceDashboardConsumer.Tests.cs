using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Charges.Positions;
using ShikshaMitra.Library.Dashboard;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

// Test-only activation bundle; root coordinates copying into the compiled graph.
// Uses an additive wire contract, retaining existing local assessment fields.
public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Dashboard_Finance_Position_Should_Show_Actual_Partial_Settlement_With_Only_Dashboard_Authority()
	{
		var fixture = await SeedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		var owner = fixture.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var denied = await RestrictConsumerToDashboardAsync(owner);
		await AssertDashboardConsumerAuthorityAsync(owner, denied);
		// Actual Finance query precondition: no invented projection or mocked amount.
		var service = ConsumerPositionService(owner);
		await WithUnitOfWorkAsync(async () =>
		{
			var query = await service.GetQueryAsync(new(LibraryChargePositionReadPurpose.Dashboard, owner.YearId));
			var actual = await GetRequiredService<Volo.Abp.Linq.IAsyncQueryableExecuter>().SingleAsync(query);
			actual.Settled.ShouldBe(40.25m);
			actual.FinancialOutstanding.ShouldBe(60.50m);
		});
		var overview = await WithUnitOfWorkAsync(() => ConsumerDashboardService(owner).GetOverviewAsync(owner.YearId));
		overview.Charges.PendingCount.ShouldBe(1); overview.Charges.PendingAmount.ShouldBe(100.75m);
		overview.Charges.PaidCount.ShouldBe(0); overview.Charges.WaivedCount.ShouldBe(0);
		var json = JsonSerializer.SerializeToElement(overview, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
		json.TryGetProperty("financePosition", out var summary).ShouldBeTrue(
			"The dashboard must expose verified settlement evidence separately from original/local charge declarations.");
		summary.GetProperty("verifiedCount").GetInt32().ShouldBe(1);
		summary.GetProperty("needsReviewCount").GetInt32().ShouldBe(0);
		summary.GetProperty("unpostedCount").GetInt32().ShouldBe(0);
		var totals = summary.GetProperty("currencyTotals").EnumerateArray().ShouldHaveSingleItem();
		totals.GetProperty("currency").GetString().ShouldBe("INR");
		totals.GetProperty("originalAmountText").GetString().ShouldBe("100.75");
		totals.GetProperty("settledAmountText").GetString().ShouldBe("40.25");
		totals.GetProperty("pendingVerificationAmountText").GetString().ShouldBe("0.00");
		totals.GetProperty("financialOutstandingAmountText").GetString().ShouldBe("60.50");
		totals.GetProperty("collectibleAmountText").GetString().ShouldBe("60.50");
	}

	private LibraryDashboardAppService ConsumerDashboardService(Fixture owner)
	{
		var services = new DashboardConsumerServices(new FinanceServices(ServiceProvider, owner, useStoredSource: true));
		var service = ActivatorUtilities.CreateInstance<LibraryDashboardAppService>(services);
		service.LazyServiceProvider = new AbpLazyServiceProvider(services);
		return service;
	}

	private sealed class DashboardConsumerServices(IServiceProvider inner) : IServiceProvider
	{
		public object? GetService(Type type) => type == typeof(ILibraryChargePositionReader)
			? ActivatorUtilities.CreateInstance<LibraryChargePositionReader>(inner)
			: inner.GetService(type);
	}
}

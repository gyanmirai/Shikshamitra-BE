using System;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Charges;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Dashboard_Collection_Alert_Should_Offer_Review_Instead_Of_Debt_For_An_Unposted_Legacy_Declaration()
	{
		var owner = await SeedAsync(FinanceExternalPayerKind.Employee, departed: false);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var raw = JsonSerializer.Serialize(await ConsumerRawSourceAsync(owner.AssessmentId));
		var denied = await RestrictConsumerToDashboardAsync(owner);
		await AssertDashboardConsumerAuthorityAsync(owner, denied);
		var position = await DashboardAlertPositionAsync(owner, null);
		position.Evidence.ShouldBe(FinanceExternalPositionEvidence.Unposted);
		position.AssessmentOriginal.ShouldBe(100m); position.SourceLinkId.ShouldBeNull();
		position.Currency.ShouldBeNull(); position.AcademicYearId.ShouldBeNull();
		position.Collectible.ShouldBeNull(); position.Settled.ShouldBeNull();
		await AssertNoTypedCollectionsAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>().CountAsync()).ShouldBe(0);
			(await GetRequiredService<IRepository<Charge, Guid>>().CountAsync()).ShouldBe(0);
		});

		var overview = await WithUnitOfWorkAsync(() => ConsumerDashboardService(owner).GetOverviewAsync());

		overview.Charges.PendingCount.ShouldBe(1); overview.Charges.PendingAmount.ShouldBe(100m);
		overview.FinancePosition.UnpostedCount.ShouldBe(1); overview.FinancePosition.VerifiedCount.ShouldBe(0);
		overview.FinancePosition.CurrencyTotals.ShouldBeEmpty();
		AssertNoDashboardCollectionAlerts(overview);
		AssertDashboardFinancialReviewNotice(overview, "unpostedCharges");
		JsonSerializer.Serialize(await ConsumerRawSourceAsync(owner.AssessmentId)).ShouldBe(raw);
		await AssertNoTypedCollectionsAsync();
	}

	[Fact]
	public async Task Dashboard_Collection_Alert_Should_Offer_Review_For_Genuine_Old_Source_Evidence_After_Category_Rename()
	{
		var fixture = await SeedLegacyOriginPostAsync();
		var owner = fixture.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var initial = await ConsumerPositionAsync(owner);
		initial.Evidence.ShouldBe(FinanceExternalPositionEvidence.Verified);
		var before = await CaptureDashboardClockSourceAsync(owner);
		var link = await ReadLegacyOriginLinkAsync(owner);
		using (var json = JsonDocument.Parse(link.SourceSnapshotJson))
			json.RootElement.TryGetProperty("ReasonOrigin", out _).ShouldBeFalse();
		// An actual supported category rename changes current display context.
		// No bound assessment, raw date, accepted snapshot or fingerprint is edited.
		await RenameLegacyOriginCategoryAsync(owner);
		(await CaptureDashboardClockSourceAsync(owner)).ShouldBe(before);
		var denied = await RestrictConsumerToDashboardAsync(owner);
		await AssertDashboardConsumerAuthorityAsync(owner, denied);
		var position = await DashboardAlertPositionAsync(owner, owner.YearId);
		position.Evidence.ShouldBe(FinanceExternalPositionEvidence.NeedsReview);
		position.Collectible.ShouldBeNull(); position.Original.ShouldBeNull(); position.Settled.ShouldBeNull();
		await AssertNoTypedCollectionsAsync();

		var overview = await WithUnitOfWorkAsync(() => ConsumerDashboardService(owner).GetOverviewAsync(owner.YearId));

		overview.Charges.PendingCount.ShouldBe(1); overview.Charges.PendingAmount.ShouldBe(100m);
		overview.FinancePosition.NeedsReviewCount.ShouldBe(1); overview.FinancePosition.VerifiedCount.ShouldBe(0);
		overview.FinancePosition.CurrencyTotals.ShouldBeEmpty();
		AssertNoDashboardCollectionAlerts(overview);
		AssertDashboardFinancialReviewNotice(overview, "reviewCharges");
		(await CaptureDashboardClockSourceAsync(owner)).ShouldBe(before);
		await AssertNoTypedCollectionsAsync();
	}
}

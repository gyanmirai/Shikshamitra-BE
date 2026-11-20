using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Accounts.CashManagement;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Typed_Moved_Employee_Cash_Detail_Should_Retain_Original_Recorded_Source_Authority()
	{
		var fixture = await SeedMovedTypedCashAsync();
		var owner = fixture.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		await SetMovedCashReaderScopeAsync(fixture, currentPlacement: false);
		await AssertMovedCashAuthorityAsync(fixture, currentPlacement: false);

		var detail = await WithUnitOfWorkAsync(() => MovedCashService(owner).GetAsync(fixture.CashId, owner.YearId));

		detail.Id.ShouldBe(fixture.CashId);
		detail.SourceId.ShouldBe(fixture.Collection.CollectionId);
		detail.StudentId.ShouldBeNull();
		detail.FinanceFeeAccountId.ShouldBeNull();
		detail.Amount.ShouldBe(40.25m);
		detail.RemainingAmount.ShouldBe(40.25m);
		detail.Status.ShouldBe(0);
		await AssertMovedCashOriginalSourceIntactAsync(fixture);
	}

	[Fact]
	public async Task Typed_Moved_Employee_Cash_Detail_Should_Deny_Current_Placement_And_Unrelated_Student_Authority()
	{
		var fixture = await SeedMovedTypedCashAsync();
		var owner = fixture.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		await SetMovedCashReaderScopeAsync(fixture, currentPlacement: true);
		await AssertMovedCashAuthorityAsync(fixture, currentPlacement: true);

		var denied = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
			MovedCashService(owner).GetAsync(fixture.CashId, owner.YearId)));

		denied.Code.ShouldBe(CashManagementErrorCodes.InvalidSource);
		await AssertMovedCashOriginalSourceIntactAsync(fixture);
	}

	[Fact]
	public async Task Typed_Moved_Employee_Cash_List_Should_Include_The_Recorded_Source_In_Counts_Summaries_And_Pages()
	{
		var fixture = await SeedMovedTypedCashAsync();
		var owner = fixture.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		await SetMovedCashReaderScopeAsync(fixture, currentPlacement: false);
		await AssertMovedCashAuthorityAsync(fixture, currentPlacement: false);
		var service = MovedCashService(owner);

		var first = await WithUnitOfWorkAsync(() => service.GetListAsync(new() { AcademicYearId = owner.YearId, Page = 1, PageSize = 1 }));
		var second = await WithUnitOfWorkAsync(() => service.GetListAsync(new() { AcademicYearId = owner.YearId, Page = 2, PageSize = 1 }));

		first.TotalCount.ShouldBe(1);
		first.Items.Single().Id.ShouldBe(fixture.CashId);
		first.Items.Single().SourceId.ShouldBe(fixture.Collection.CollectionId);
		first.Summary.Collected.ShouldBe(40.25m);
		first.YearSummary.Collected.ShouldBe(40.25m);
		first.AvailableCash.ShouldBe(40.25m);
		first.StatusCounts.All.ShouldBe(1);
		first.StatusCounts.Pending.ShouldBe(1);
		second.TotalCount.ShouldBe(1);
		second.Items.ShouldBeEmpty();
		second.Summary.Collected.ShouldBe(40.25m);
		await AssertMovedCashOriginalSourceIntactAsync(fixture);
	}

	[Fact]
	public async Task Typed_Moved_Employee_Cash_List_Should_Exclude_Current_Placement_Only_Source_Before_Counts_And_Summaries()
	{
		var fixture = await SeedMovedTypedCashAsync();
		var owner = fixture.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		await SetMovedCashReaderScopeAsync(fixture, currentPlacement: true);
		await AssertMovedCashAuthorityAsync(fixture, currentPlacement: true);
		var service = MovedCashService(owner);

		var first = await WithUnitOfWorkAsync(() => service.GetListAsync(new() { AcademicYearId = owner.YearId, Page = 1, PageSize = 1 }));
		var second = await WithUnitOfWorkAsync(() => service.GetListAsync(new() { AcademicYearId = owner.YearId, Page = 2, PageSize = 1 }));

		foreach (var page in new[] { first, second })
		{
			page.TotalCount.ShouldBe(0);
			page.Items.ShouldBeEmpty();
			page.Summary.Collected.ShouldBe(0m);
			page.YearSummary.Collected.ShouldBe(0m);
			page.AvailableCash.ShouldBe(0m);
			page.StatusCounts.All.ShouldBe(0);
			page.ActivitySummary.Collected.ShouldBe(0m);
		}
		await AssertMovedCashOriginalSourceIntactAsync(fixture);
	}
}


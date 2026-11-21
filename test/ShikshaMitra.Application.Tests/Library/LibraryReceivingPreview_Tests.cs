using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Receiving;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryReceivingPreview_Tests : ShikshaMitraApplicationTestBase<LibraryReceivingPreviewTestModule>
{
	[Theory]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.Receiving.View)]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.Receiving.Add)]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.AllBooks.View)]
	public async Task Receiving_Preview_Should_Require_Every_Exact_Action_Even_For_Direct_Application_Calls(string removed)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		GetRequiredService<IPermissionChecker>().IsGrantedAsync(removed).Returns(false);
		await Should.ThrowAsync<AbpAuthorizationException>(() => Service().PreviewAsync(new()));
	}

	[Fact]
	public async Task Receiving_Preview_Should_Require_An_Actual_School_Before_Reading_Catalogue_Data()
	{
		using var host = GetRequiredService<ICurrentTenant>().Change(null);
		var error = await Should.ThrowAsync<BusinessException>(() => Service().PreviewAsync(new()));
		error.Code.ShouldBe(ShikshaMitra.Security.UserScope.UserScopeErrorCodes.AccessDenied);
	}

	[Fact]
	public async Task Receiving_Preview_Should_Show_Unknown_Donation_Cost_Without_Creating_Any_Copy()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var input = Input(context);
		var preview = await Service().PreviewAsync(input);
		preview.CanReceive.ShouldBeTrue(); preview.Revision.ShouldNotBeNullOrWhiteSpace();
		preview.UnknownCostCopyCount.ShouldBe(2); preview.KnownCostCopyCount.ShouldBe(0);
		preview.KnownTotals.ShouldBeEmpty(); preview.Errors.ShouldBeEmpty();
		var line = preview.Lines.Single();
		line.BookId.ShouldBe(context.BookId); line.BookTitle.ShouldBe("Persistent library title");
		line.Quantity.ShouldBe(2); line.UnitCost.ShouldBeNull(); line.Currency.ShouldBeNull();
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<BookCopy, Guid>>().GetListAsync(row => row.BookId == context.BookId)).Count.ShouldBe(1));
		input.Lines[0].Accessions.ShouldBeNull("the server must not fabricate accepted labels while previewing");
	}

	[Fact]
	public async Task Receiving_Preview_Should_Preserve_Exact_Cost_And_Separate_Known_Currency_Totals()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var input = Input(context); input.Source = LibraryStockSource.Purchase;
		input.Lines[0].UnitCost = "350.50"; input.Lines[0].Currency = "INR";
		var preview = await Service().PreviewAsync(input);
		preview.CanReceive.ShouldBeTrue(); preview.KnownCostCopyCount.ShouldBe(2); preview.UnknownCostCopyCount.ShouldBe(0);
		preview.Lines.Single().UnitCost.ShouldBe("350.50");
		preview.KnownTotals.Single().Currency.ShouldBe("INR"); preview.KnownTotals.Single().Amount.ShouldBe("701.00");
	}

	[Fact]
	public async Task Receiving_Preview_Should_Report_A_Bad_Later_Line_Without_Changing_Valid_Stock()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var input = Input(context);
		input.Lines.Add(new() { BookId = context.BookId, SectionId = context.SectionId, ShelfId = Guid.NewGuid(), Quantity = 1 });
		var preview = await Service().PreviewAsync(input);
		preview.CanReceive.ShouldBeFalse(); preview.Revision.ShouldBeNull();
		preview.Errors.ShouldContain(error => error.LineIndex == 1);
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<BookCopy, Guid>>().GetListAsync(row => row.BookId == context.BookId)).Count.ShouldBe(1));
	}

	[Theory]
	[InlineData("0.001", "INR")]
	[InlineData("1,200.00", "INR")]
	[InlineData("5.25", null)]
	[InlineData(null, "INR")]
	public async Task Receiving_Preview_Should_Return_A_Line_Error_For_Unconfirmed_Or_Invalid_Cost(string? cost, string? currency)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var input = Input(context); input.Lines[0].UnitCost = cost; input.Lines[0].Currency = currency;
		var preview = await Service().PreviewAsync(input);
		preview.CanReceive.ShouldBeFalse(); preview.Errors.ShouldContain(error => error.LineIndex == 0);
		preview.Revision.ShouldBeNull();
	}

	private ILibraryReceivingAppService Service() => GetRequiredService<ILibraryReceivingAppService>();
	private static LibraryReceivingInput Input(LibraryPolicyTestContext context) => new()
	{
		ReceivedOn = new(2026, 11, 18), Source = LibraryStockSource.Donation, SourceName = "Community donor",
		Lines = [new() { BookId = context.BookId, SectionId = context.SectionId, ShelfId = context.ShelfId, Quantity = 2 }]
	};
}

[DependsOn(typeof(ShikshaMitraApplicationTestModule))]
public class LibraryReceivingPreviewTestModule : AbpModule
{
	public override void ConfigureServices(ServiceConfigurationContext context)
	{
		var permissions = Substitute.For<IPermissionChecker>();
		permissions.IsGrantedAsync(Arg.Any<string>()).Returns(false);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Receiving.View).Returns(true);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Receiving.Add).Returns(true);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.View).Returns(true);
		context.Services.Replace(ServiceDescriptor.Singleton(permissions));
	}
}

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryCatalogEditorOptions_Tests : ShikshaMitraApplicationTestBase<LibraryCatalogEditorOptionsTestModule>
{
	[Fact]
	public async Task Catalogue_Add_Grant_Should_Provide_Permanent_Editor_Options_Without_Settings_Grants()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		Permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update).Returns(false);
		(await Permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_BookCategories.View)).ShouldBeFalse();
		(await Permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_SectionManagement.View)).ShouldBeFalse();

		var options = await Books.GetEditorOptionsAsync(context.Catalog.YearId);

		options.BookCategories.ShouldContain(row => row.Id == context.Catalog.CategoryId && !string.IsNullOrWhiteSpace(row.Name));
		var section = options.Sections.Single(row => row.Id == context.Catalog.SectionId);
		section.SectionName.ShouldBe("Main Library");
		section.ShelfDetails.ShouldContain(row => row.Id == context.Catalog.ShelfId && row.ShelfName == "A1");
	}

	[Fact]
	public async Task Editor_Options_Should_Allow_Update_Only_And_Require_View_And_At_Least_One_Editing_Grant()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		Permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Add).Returns(false);

		(await Books.GetEditorOptionsAsync(context.Catalog.YearId)).BookCategories.ShouldContain(row => row.Id == context.Catalog.CategoryId);
		Permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update).Returns(false);
		(await Permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Add)).ShouldBeFalse();
		(await Permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update)).ShouldBeFalse();
		await Should.ThrowAsync<AbpAuthorizationException>(() => Books.GetEditorOptionsAsync(context.Catalog.YearId));
		Permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update).Returns(true);
		Permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.View).Returns(false);
		(await Permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.View))
			.ShouldBeFalse("the test must deny the exact catalogue View grant before exercising the command");
		await Should.ThrowAsync<AbpAuthorizationException>(() => Books.GetEditorOptionsAsync(context.Catalog.YearId));
	}

	[Fact]
	public async Task Editor_Options_Should_Require_An_Existing_Authorized_Year_And_Actual_User_Scope()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		await Should.ThrowAsync<BusinessException>(() => Books.GetEditorOptionsAsync(Guid.NewGuid()));
		await ReplaceScopeAsync(context.Catalog.OriginYearId);
		await Should.ThrowAsync<BusinessException>(() => Books.GetEditorOptionsAsync(context.Catalog.YearId));
		await ReplaceScopeAsync(null);
		await Should.ThrowAsync<BusinessException>(() => Books.GetEditorOptionsAsync(context.Catalog.OriginYearId));
	}

	[Fact]
	public async Task Permanent_Editor_Options_Should_Exclude_Foreign_Tenant_Categories_Sections_And_Shelves()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		LibraryCopyLifecycleTestContext foreign;
		using (LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider)) foreign = await SeedAsync();

		var options = await Books.GetEditorOptionsAsync(context.Catalog.YearId);

		options.BookCategories.Select(row => row.Id).ShouldContain(context.Catalog.CategoryId);
		options.BookCategories.Select(row => row.Id).ShouldNotContain(foreign.Catalog.CategoryId);
		options.Sections.Select(row => row.Id).ShouldContain(context.Catalog.SectionId);
		options.Sections.Select(row => row.Id).ShouldNotContain(foreign.Catalog.SectionId);
		options.Sections.SelectMany(row => row.ShelfDetails).Select(row => row.Id).ShouldNotContain(foreign.Catalog.ShelfId);
		await Should.ThrowAsync<BusinessException>(() => Books.GetEditorOptionsAsync(foreign.Catalog.YearId));
	}

	private IBookAppService Books => GetRequiredService<IBookAppService>();
	private IPermissionChecker Permissions => GetRequiredService<IPermissionChecker>();
	private Task<LibraryCopyLifecycleTestContext> SeedAsync() =>
		WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));

	private Task ReplaceScopeAsync(Guid? yearId) => WithUnitOfWorkAsync(async () =>
	{
		var userId = GetRequiredService<ICurrentUser>().Id!.Value;
		var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		await assignments.DeleteAsync(row => row.UserId == userId, true);
		if (yearId.HasValue)
			await assignments.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id,
				userId: userId, academicYearId: yearId.Value), true);
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(userId);
	});
}

[DependsOn(typeof(ShikshaMitraApplicationTestModule))]
public class LibraryCatalogEditorOptionsTestModule : AbpModule
{
	public override void ConfigureServices(ServiceConfigurationContext context)
	{
		var permissions = Substitute.For<IPermissionChecker>();
		permissions.IsGrantedAsync(Arg.Any<string>()).Returns(true);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_BookCategories.View).Returns(false);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_SectionManagement.View).Returns(false);
		context.Services.Replace(ServiceDescriptor.Singleton(permissions));
	}
}

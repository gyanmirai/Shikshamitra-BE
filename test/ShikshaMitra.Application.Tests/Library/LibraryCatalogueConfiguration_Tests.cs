using System;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Catalogue;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCatalogueConfiguration_Tests : ShikshaMitraApplicationTestBase<LibraryCatalogEditorOptionsTestModule>
{
	private ILibraryCatalogueConfigurationAppService Configuration => GetRequiredService<ILibraryCatalogueConfigurationAppService>();
	private IBookAppService Books => GetRequiredService<IBookAppService>();
	private IPermissionChecker Permissions => GetRequiredService<IPermissionChecker>();

	[Fact]
	public async Task Catalogue_Configuration_Should_Project_Existing_Defaults_Through_Owned_Editor_Options()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var configuration = await Configuration.GetAsync();
		configuration.Source.ShouldBe("Defaults");
		configuration.Revision.ShouldBe("defaults-v1");
		configuration.RequiredFields.ShouldBe(["author", "language"]);
		configuration.Materials.ShouldBeEmpty();
		configuration.Languages.Count.ShouldBe(5);
		configuration.CanManageConfiguration.ShouldBeTrue();
		Permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_CatalogueConfiguration.View).Returns(false);
		Permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_CatalogueConfiguration.Update).Returns(false);

		var options = await Books.GetEditorOptionsAsync(context.YearId);

		options.CatalogueConfiguration.ShouldNotBeNull();
		options.CatalogueConfiguration.Revision.ShouldBe(configuration.Revision);
		options.CatalogueConfiguration.CanManageConfiguration.ShouldBeFalse();
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<Book, Guid>>().GetAsync(context.BookId)).Author.ShouldBe("School Author"));
	}

	[Theory]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.Settings_CatalogueConfiguration.View)]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.Settings_CatalogueConfiguration.Update)]
	public async Task Catalogue_Configuration_Write_Should_Require_Both_Exact_Actions(string denied)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		await SeedAsync();
		Permissions.IsGrantedAsync(denied).Returns(false);
		await Should.ThrowAsync<AbpAuthorizationException>(() => Configuration.UpdateAsync(new()
		{
			ExpectedRevision = "defaults-v1", Definition = Definition()
		}));
	}

	[Fact]
	public async Task Catalogue_Configuration_View_Alone_Should_Read_Without_Offering_Management()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		await SeedAsync();
		Permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_CatalogueConfiguration.Update).Returns(false);
		(await Configuration.GetAsync()).CanManageConfiguration.ShouldBeFalse();
		Permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_CatalogueConfiguration.View).Returns(false);
		await Should.ThrowAsync<AbpAuthorizationException>(() => Configuration.GetAsync());
	}

	[Fact]
	public async Task Schoolwide_Catalogue_Rules_Should_Require_Tenantwide_Management_And_Current_Scope_For_Reads()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		await ReplaceScopeAsync(context.YearId);
		(await GetRequiredService<ILibraryAccessService>().HasTenantWideScopeAsync()).ShouldBeFalse();
		(await Configuration.GetAsync()).CanManageConfiguration.ShouldBeFalse();
		var denied = await Should.ThrowAsync<BusinessException>(() => Configuration.UpdateAsync(new()
		{
			ExpectedRevision = "defaults-v1", Definition = Definition()
		}));
		denied.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		await ReplaceScopeAsync(null);
		(await Should.ThrowAsync<BusinessException>(() => Configuration.GetAsync())).Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
	}

	[Fact]
	public async Task An_Anonymous_Catalogue_Rule_Should_Apply_Only_To_Its_School_And_Not_Rewrite_Old_Books()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var definition = Definition(); definition.RequiredFields.Remove("author");
		var saved = await Configuration.UpdateAsync(new() { ExpectedRevision = "defaults-v1", Definition = definition });
		saved.Source.ShouldBe("School"); saved.Revision.ShouldNotBe("defaults-v1");
		var created = await Books.CreateAsync(context.YearId, Input(context, saved.Revision, author: string.Empty));
		var createdId = Guid.Parse(created.Id);
		(await Books.GetAsync(createdId)).Author.ShouldBeEmpty();
		(await Books.GetAsync(context.BookId)).Author.ShouldBe("School Author");
		using (LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider))
		{
			var other = await SeedAsync();
			var otherDefaults = await Configuration.GetAsync();
			otherDefaults.Source.ShouldBe("Defaults"); otherDefaults.RequiredFields.ShouldContain("author");
			await Should.ThrowAsync<BusinessException>(() => Books.CreateAsync(other.YearId, Input(other, otherDefaults.Revision, author: string.Empty)));
		}
		(await Configuration.GetAsync()).Revision.ShouldBe(saved.Revision);
	}

	[Fact]
	public async Task A_Stale_School_Configuration_Save_And_Stale_Catalogue_Editor_Should_Not_Overwrite_Or_Create_Stock()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var definition = Definition(); definition.RequiredFields.Remove("author");
		var saved = await Configuration.UpdateAsync(new() { ExpectedRevision = "defaults-v1", Definition = definition });
		var changed = Definition(); changed.RequiredFields.Add("publisher");
		var latest = await Configuration.UpdateAsync(new() { ExpectedRevision = saved.Revision, Definition = changed });
		await Should.ThrowAsync<BusinessException>(() => Configuration.UpdateAsync(new()
		{
			ExpectedRevision = saved.Revision, Definition = definition
		}));
		await Should.ThrowAsync<BusinessException>(() => Books.CreateAsync(context.YearId, Input(context, saved.Revision)));
		await Should.ThrowAsync<BusinessException>(() => Books.CreateAsync(context.YearId, Input(context, revision: null)));
		(await Configuration.GetAsync()).Revision.ShouldBe(latest.Revision);
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<Book, Guid>>().CountAsync()).ShouldBe(1);
			(await GetRequiredService<IRepository<BookCopy, Guid>>().CountAsync()).ShouldBe(1);
		});
	}

	private Task<LibraryPolicyTestContext> SeedAsync() =>
		WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));

	private Task ReplaceScopeAsync(Guid? year) => WithUnitOfWorkAsync(async () =>
	{
		var actor = GetRequiredService<ICurrentUser>().Id!.Value;
		var tenant = GetRequiredService<ICurrentTenant>().Id;
		var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		await assignments.DeleteAsync(row => row.TenantId == tenant && row.UserId == actor, true);
		if (year.HasValue) await assignments.InsertAsync(new(Guid.NewGuid(), tenant, userId: actor, academicYearId: year.Value), true);
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(actor);
	});

	private static CreateBookRequest Input(LibraryPolicyTestContext context, string? revision, string author = "Author") => new()
	{
		Title = "New catalogue title", Author = author, CategoryId = context.CategoryId, Language = Language.English,
		ExpectedCatalogueConfigurationRevision = revision, NumberOfCopies = 0, Price = 100.25m, Pages = 20
	};

	private static LibraryCatalogueDefinition Definition() => new()
	{
		RequiredFields = ["author", "language"],
		Languages = [new() { Code = "gu", Label = "Gujarati" }, new() { Code = "hi", Label = "Hindi" },
			new() { Code = "en", Label = "English" }, new() { Code = "mr", Label = "Marathi" }, new() { Code = "sa", Label = "Sanskrit" }],
		Materials = []
	};
}

using System;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Receiving;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryReceivingOptions_Tests : ShikshaMitraApplicationTestBase<LibraryReceivingPreviewTestModule>
{
	[Fact]
	public async Task Receiving_Options_Should_Use_Owned_Permanent_Locations_Without_Catalogue_Write_Or_Setup_Grants()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var owned = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		Guid foreignSection;
		using (LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider))
			foreignSection = (await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider))).SectionId;
		var permissions = GetRequiredService<IPermissionChecker>();
		(await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Add)).ShouldBeFalse();
		(await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update)).ShouldBeFalse();
		(await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_SectionManagement.View)).ShouldBeFalse();
		owned.OriginYearId.ShouldNotBe(owned.YearId);

		var options = await GetRequiredService<ILibraryReceivingAppService>().GetOptionsAsync();

		options.Sections.ShouldContain(section => section.Id == owned.SectionId &&
			section.ShelfDetails.Exists(shelf => shelf.Id == owned.ShelfId));
		options.Sections.ShouldNotContain(section => section.Id == foreignSection);
	}

	[Theory]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.Receiving.View)]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.Receiving.Add)]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.AllBooks.View)]
	public async Task Receiving_Options_Should_Require_Its_Exact_Actions_For_A_Direct_Application_Call(string missing)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		GetRequiredService<IPermissionChecker>().IsGrantedAsync(missing).Returns(false);
		await Should.ThrowAsync<AbpAuthorizationException>(() => GetRequiredService<ILibraryReceivingAppService>().GetOptionsAsync());
	}
}

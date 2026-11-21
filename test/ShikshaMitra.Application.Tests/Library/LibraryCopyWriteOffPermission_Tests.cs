using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryCopyWriteOffPermission_Tests : ShikshaMitraApplicationTestBase<LibraryCopyLifecyclePermissionTestModule>
{
	[Fact]
	public async Task Catalogue_Update_Grant_Should_Not_Be_Sufficient_For_Legacy_Write_Off()
	{
		(await GetRequiredService<IPermissionChecker>().IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Delete)).ShouldBeFalse();
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
		var copy = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(context.Catalog.CopyId));
		var state = await GetRequiredService<ILibraryCopyLifecycleAppService>().GetStateAsync(copy.BookId, copy.Id);

		await Should.ThrowAsync<AbpAuthorizationException>(() => GetRequiredService<IBookAppService>().UpdateCopyStatusAsync(
			copy.BookId, copy.AccessionNumber, new() { Status = BookCopyStatus.Discarded, Remarks = "Beyond repair", Revision = state.Revision }));
	}

	[Fact]
	public async Task Server_Write_Off_Capability_And_Command_Should_Require_The_Delete_Grant()
	{
		(await GetRequiredService<IPermissionChecker>().IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Delete)).ShouldBeFalse();
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
		var lifecycle = ServiceProvider.GetService<ILibraryCopyLifecycleAppService>();
		lifecycle.ShouldNotBeNull();
		var state = await lifecycle.GetStateAsync(context.Catalog.BookId, context.Catalog.CopyId);

		state.CanChangeCondition.ShouldBeTrue();
		state.CanWriteOff.ShouldBeFalse();
		await Should.ThrowAsync<AbpAuthorizationException>(() => lifecycle.WriteOffAsync(state.BookId, state.CopyId,
			new() { Reason = "Beyond repair", Revision = state.Revision }));
	}
}

[DependsOn(typeof(ShikshaMitraApplicationTestModule))]
public class LibraryCopyLifecyclePermissionTestModule : AbpModule
{
	public override void ConfigureServices(ServiceConfigurationContext context)
	{
		var permissions = Substitute.For<IPermissionChecker>();
		permissions.IsGrantedAsync(Arg.Any<string>()).Returns(true);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Delete).Returns(false);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_SectionManagement.View).Returns(false);
		context.Services.Replace(ServiceDescriptor.Singleton(permissions));
	}
}

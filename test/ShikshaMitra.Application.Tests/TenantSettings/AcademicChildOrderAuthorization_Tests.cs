using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class AcademicChildOrderAuthorization_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Direct_reorder_calls_require_academic_setup_edit_before_storage_access(bool reorderDivisions)
	{
		var authorization = Substitute.For<IAbpAuthorizationService>();
		authorization.CurrentPrincipal.Returns(new ClaimsPrincipal(new ClaimsIdentity()));
		authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
			.Returns(Task.FromResult(AuthorizationResult.Failed()));
		using var services = new ServiceCollection().AddSingleton<IAuthorizationService>(authorization).BuildServiceProvider();
		var provider = new AbpLazyServiceProvider(services);
		var standardService = new StandardAppService(null!, null!, null!, null!, null!, null!, null!) { LazyServiceProvider = provider };
		var divisionService = new DivisionAppService(null!, null!, null!, null!, null!) { LazyServiceProvider = provider };
		Func<Task> action = reorderDivisions
			? () => divisionService.ReorderAsync(Guid.NewGuid(), new() { Ids = [Guid.NewGuid()] })
			: () => standardService.ReorderAsync(Guid.NewGuid(), new() { Ids = [Guid.NewGuid()] });
		(await Should.ThrowAsync<AbpAuthorizationException>(action)).Data["PolicyName"]
			.ShouldBe(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.Edit);
	}
}

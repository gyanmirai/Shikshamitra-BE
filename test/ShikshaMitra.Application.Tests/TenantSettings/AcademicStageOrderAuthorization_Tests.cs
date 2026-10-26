using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationalMediums;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class AcademicStageOrderAuthorization_Tests
{
	[Fact]
	public async Task Direct_stage_reorder_requires_edit_before_storage_access()
	{
		var authorization = Substitute.For<IAbpAuthorizationService>();
		authorization.CurrentPrincipal.Returns(new ClaimsPrincipal(new ClaimsIdentity()));
		authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
			.Returns(Task.FromResult(AuthorizationResult.Failed()));
		using var services = new ServiceCollection().AddSingleton<IAuthorizationService>(authorization).BuildServiceProvider();
		var service = new EducationMediumStageAppService(null!, null!, null!, null!)
		{
			LazyServiceProvider = new AbpLazyServiceProvider(services)
		};
		(await Should.ThrowAsync<AbpAuthorizationException>(() => service.ReorderAsync(Guid.NewGuid(), new() { Ids = [Guid.NewGuid()] })))
			.Data["PolicyName"].ShouldBe(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.Edit);
	}
}

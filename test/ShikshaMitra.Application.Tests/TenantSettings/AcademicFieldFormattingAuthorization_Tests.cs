using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.AcademicSetups.Formatting;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class AcademicFieldFormattingAuthorization_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Direct_calls_require_the_exact_action_before_storage_access(bool write)
	{
		using var services = Services(authorized: false);
		var service = new AcademicFieldFormattingAppService(null!) { LazyServiceProvider = new AbpLazyServiceProvider(services) };
		var error = await Should.ThrowAsync<AbpAuthorizationException>(() => write ? service.UpdateAsync(new()) : service.GetAsync());
		error.Data["PolicyName"].ShouldBe(write ? ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.Edit : ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.View);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Direct_host_calls_are_denied_before_storage_access(bool write)
	{
		using var services = Services(authorized: true);
		var service = new AcademicFieldFormattingAppService(null!) { LazyServiceProvider = new AbpLazyServiceProvider(services) };
		await Should.ThrowAsync<AbpAuthorizationException>(() => write ? service.UpdateAsync(new()) : service.GetAsync());
	}

	private static ServiceProvider Services(bool authorized)
	{
		var authorization = Substitute.For<IAbpAuthorizationService>();
		authorization.CurrentPrincipal.Returns(new ClaimsPrincipal(new ClaimsIdentity()));
		authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
			.Returns(Task.FromResult(authorized ? AuthorizationResult.Success() : AuthorizationResult.Failed()));
		var tenant = Substitute.For<ICurrentTenant>();
		tenant.Id.Returns((Guid?)null);
		return new ServiceCollection().AddSingleton<IAuthorizationService>(authorization).AddSingleton(tenant).BuildServiceProvider();
	}
}

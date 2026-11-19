using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.TenantSettings.Formatting;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class InstituteFieldFormattingAuthorization_Tests
{
	[Theory]
	[InlineData("buildings", "ShikshaMitra.Module.InstituteSetup.BuildingManagement")]
	[InlineData("roomTypes", "ShikshaMitra.Module.InstituteSetup.RoomTypeManagement")]
	[InlineData("rooms", "ShikshaMitra.Module.InstituteSetup.RoomManagement.Room")]
	[InlineData("shifts", "InstituteSetup.ShiftManagement")]
	[InlineData("schoolIdentity", "InstituteSetup.SchoolIdentity")]
	[InlineData("schoolDocuments", "InstituteSetup.SchoolDocuments")]
	public async Task Direct_calls_enforce_owning_scope_before_accessing_settings(string scope, string prefix)
	{
		using var services = Services(authorized: false);
		var service = new InstituteFieldFormattingAppService(null!) { LazyServiceProvider = new AbpLazyServiceProvider(services) };
		var read = await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetAsync(scope));
		read.Data["PolicyName"].ShouldBe(prefix + ".View");
		var preview = await Should.ThrowAsync<AbpAuthorizationException>(() => service.PreviewAsync(scope, new()));
		preview.Data["PolicyName"].ShouldBe(prefix + ".View");
		var write = await Should.ThrowAsync<AbpAuthorizationException>(() => service.UpdateAsync(scope, new()));
		write.Data["PolicyName"].ShouldBe(prefix + (scope == "schoolIdentity" ? ".ManageDefaults" : scope == "shifts" ? ".Edit" : ".Add"));
	}

	[Fact]
	public async Task Direct_host_calls_cannot_read_write_or_preview_tenant_settings()
	{
		using var services = Services(authorized: true);
		var service = new InstituteFieldFormattingAppService(null!) { LazyServiceProvider = new AbpLazyServiceProvider(services) };
		await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetAsync("rooms"));
		await Should.ThrowAsync<AbpAuthorizationException>(() => service.UpdateAsync("rooms", new()));
		await Should.ThrowAsync<AbpAuthorizationException>(() => service.PreviewAsync("rooms", new()));
	}

	[Fact]
	public async Task School_wide_signature_formatting_requires_signature_access_in_addition_to_manage_defaults()
	{
		using var services = Services(authorized: true, denied: "InstituteSetup.SchoolIdentity.AccessSignature");
		var service = new InstituteFieldFormattingAppService(null!) { LazyServiceProvider = new AbpLazyServiceProvider(services) };
		var error = await Should.ThrowAsync<AbpAuthorizationException>(() => service.UpdateAsync("schoolIdentity", new()));
		error.Data["PolicyName"].ShouldBe("InstituteSetup.SchoolIdentity.AccessSignature");
	}

	private static ServiceProvider Services(bool authorized, string? denied = null)
	{
		var authorization = Substitute.For<IAbpAuthorizationService>();
		authorization.CurrentPrincipal.Returns(new ClaimsPrincipal(new ClaimsIdentity()));
		authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
			.Returns(call => Task.FromResult(authorized && call.ArgAt<string>(2) != denied ? AuthorizationResult.Success() : AuthorizationResult.Failed()));
		var tenant = Substitute.For<ICurrentTenant>();
		tenant.Id.Returns((Guid?)null);
		return new ServiceCollection().AddSingleton<IAuthorizationService>(authorization).AddSingleton(tenant).BuildServiceProvider();
	}
}

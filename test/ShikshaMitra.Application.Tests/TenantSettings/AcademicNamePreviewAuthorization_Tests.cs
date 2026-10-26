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
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class AcademicNamePreviewAuthorization_Tests
{
	[Theory]
	[InlineData(false, true)]
	[InlineData(true, false)]
	public async Task Direct_preview_calls_require_view_and_a_tenant(bool authorized, bool tenant)
	{
		using var services = Services(authorized, tenant);
		var service = new AcademicFieldFormattingAppService(null!) { LazyServiceProvider = new AbpLazyServiceProvider(services) };
		var error = await Should.ThrowAsync<AbpAuthorizationException>(() => service.PreviewAsync(new() { Value = "ΟΣ", TextCase = AcademicTextCase.CamelCase }));
		if (!authorized) error.Data["PolicyName"].ShouldBe(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.View);
	}

	[Fact]
	public async Task Direct_preview_does_not_use_settings_storage_and_validates_input()
	{
		using var services = Services(true, true);
		var service = new AcademicFieldFormattingAppService(null!) { LazyServiceProvider = new AbpLazyServiceProvider(services) };
		(await service.PreviewAsync(new() { Value = "ΟΣ", TextCase = AcademicTextCase.CamelCase })).Value.ShouldBe("οσ");
		await Should.ThrowAsync<AbpValidationException>(() => service.PreviewAsync(new() { Value = "value", TextCase = (AcademicTextCase)9 }));
		await Should.ThrowAsync<AbpValidationException>(() => service.PreviewAsync(new() { Value = new string('é', 513) }));
	}

	private static ServiceProvider Services(bool authorized, bool hasTenant)
	{
		var authorization = Substitute.For<IAbpAuthorizationService>();
		authorization.CurrentPrincipal.Returns(new ClaimsPrincipal(new ClaimsIdentity()));
		authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
			.Returns(Task.FromResult(authorized ? AuthorizationResult.Success() : AuthorizationResult.Failed()));
		var tenant = Substitute.For<ICurrentTenant>();
		tenant.Id.Returns(hasTenant ? Guid.NewGuid() : (Guid?)null);
		return new ServiceCollection().AddSingleton<IAuthorizationService>(authorization).AddSingleton(tenant).BuildServiceProvider();
	}
}

using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class SchoolIdentityAuthorization_Tests
{
	[Theory]
	[InlineData("read", SchoolIdentityPermissions.View)]
	[InlineData("save-defaults", SchoolIdentityPermissions.View)]
	[InlineData("save-defaults", SchoolIdentityPermissions.ManageDefaults)]
	[InlineData("save-override", SchoolIdentityPermissions.ManageOverrides)]
	[InlineData("save-organization", SchoolIdentityPermissions.ManageOverrides)]
	[InlineData("upload-organization", SchoolIdentityPermissions.ManageOverrides)]
	[InlineData("copy", SchoolIdentityPermissions.ManageOverrides)]
	[InlineData("copy", SchoolIdentityPermissions.AccessSignature)]
	[InlineData("history", SchoolIdentityPermissions.View)]
	[InlineData("download", SchoolIdentityPermissions.View)]
	[InlineData("upload", SchoolIdentityPermissions.ManageDefaults)]
	[InlineData("upload-signature", SchoolIdentityPermissions.AccessSignature)]
	[InlineData("preview", SchoolIdentityPermissions.View)]
	public async Task Direct_calls_check_exact_actions_before_reading_or_writing_storage(string action, string denied)
	{
		var service = new SchoolIdentityAppService(null!, null!, null!, null!, null!, null!, null!, null!, null!)
			{ LazyServiceProvider = Denying(denied) };
		Func<Task> operation = action switch
		{
			"read" => () => service.GetAsync(new()),
			"save-defaults" => () => service.UpdateAsync(new()),
			"save-override" => () => service.UpdateAsync(new() { BoardId = Guid.NewGuid() }),
			"save-organization" => () => service.UpdateAsync(new() { BoardId = Guid.NewGuid(), MediumId = Guid.NewGuid(), OrganizationId = Guid.NewGuid() }),
			"upload-organization" => () => service.UploadAsync(SchoolIdentityAssetKind.Banner, Guid.NewGuid(), Guid.NewGuid(), "banner.png", [], Guid.NewGuid()),
			"copy" => () => service.CopyAsync(new()),
			"history" => () => service.GetHistoryAsync(new()),
			"download" => () => service.DownloadAsync(Guid.NewGuid(), new()),
			"upload" => () => service.UploadAsync(SchoolIdentityAssetKind.Logo, null, null, "logo.png", []),
			"upload-signature" => () => service.UploadAsync(SchoolIdentityAssetKind.Signature, null, null, "sign.png", []),
			_ => () => service.PreviewAsync(new())
		};
		(await Should.ThrowAsync<AbpAuthorizationException>(operation)).Data["PolicyName"].ShouldBe(denied);
	}

	[Fact]
	public async Task Public_branding_rejects_the_signature_kind_before_storage()
	{
		var service = new SchoolIdentityAppService(null!, null!, null!, null!, null!, null!, null!, null!, null!);
		await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetBrandingAssetAsync(SchoolIdentityAssetKind.Signature));
	}

	private static AbpLazyServiceProvider Denying(string denied)
	{
		var authorization = Substitute.For<IAbpAuthorizationService>();
		authorization.CurrentPrincipal.Returns(new ClaimsPrincipal(new ClaimsIdentity()));
		authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
			.Returns(call => Task.FromResult(call.ArgAt<string>(2) == denied ? AuthorizationResult.Failed() : AuthorizationResult.Success()));
		return new AbpLazyServiceProvider(new ServiceCollection().AddSingleton<IAuthorizationService>(authorization).BuildServiceProvider());
	}
}

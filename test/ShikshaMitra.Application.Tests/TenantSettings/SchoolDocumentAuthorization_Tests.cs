using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.TenantSettings.SchoolDocuments;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class SchoolDocumentAuthorization_Tests
{
	[Theory]
	[InlineData("list", SchoolDocumentPermissions.View)]
	[InlineData("get", SchoolDocumentPermissions.View)]
	[InlineData("file", SchoolDocumentPermissions.View)]
	[InlineData("create", SchoolDocumentPermissions.View)]
	[InlineData("create", SchoolDocumentPermissions.Add)]
	[InlineData("update", SchoolDocumentPermissions.View)]
	[InlineData("update", SchoolDocumentPermissions.Add)]
	[InlineData("delete", SchoolDocumentPermissions.View)]
	[InlineData("delete", SchoolDocumentPermissions.Delete)]
	public async Task Direct_calls_require_exact_permissions_before_accessing_storage(string action, string denied)
	{
		var authorization = Substitute.For<IAbpAuthorizationService>();
		authorization.CurrentPrincipal.Returns(new ClaimsPrincipal(new ClaimsIdentity()));
		authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
			.Returns(call => Task.FromResult(call.ArgAt<string>(2) == denied ? AuthorizationResult.Failed() : AuthorizationResult.Success()));
		var service = new SchoolDocumentAppService(null!, null!, null!) { LazyServiceProvider = new AbpLazyServiceProvider(
			new ServiceCollection().AddSingleton<IAuthorizationService>(authorization).BuildServiceProvider()) };
		Func<Task> operation = action switch
		{
			"list" => () => service.GetListAsync(new()),
			"get" => () => service.GetAsync(Guid.NewGuid()),
			"file" => () => service.GetFileAsync(Guid.NewGuid(), Guid.NewGuid()),
			"create" => () => service.CreateAsync(new()),
			"update" => () => service.UpdateAsync(Guid.NewGuid(), new()),
			_ => () => service.DeleteAsync(Guid.NewGuid())
		};
		(await Should.ThrowAsync<AbpAuthorizationException>(operation)).Data["PolicyName"].ShouldBe(denied);
	}
}

using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class RoomAuthorization_Tests
{
	[Theory]
	[InlineData("get", false)]
	[InlineData("image", false)]
	[InlineData("delete", false)]
	[InlineData("list", false)]
	[InlineData("features", false)]
	[InlineData("create", true)]
	[InlineData("update", true)]
	[InlineData("add-feature", true)]
	public async Task Direct_calls_require_room_permissions_before_accessing_storage(string operation, bool write)
	{
		var denied = operation == "delete" ? ShikshaMitraSubmodulePermissions.InstituteSetup.RoomManagement.Delete
			: write ? ShikshaMitraSubmodulePermissions.InstituteSetup.RoomManagement.Add
			: ShikshaMitraSubmodulePermissions.InstituteSetup.RoomManagement.View;
		var authorization = Substitute.For<IAbpAuthorizationService>();
		authorization.CurrentPrincipal.Returns(new ClaimsPrincipal(new ClaimsIdentity()));
		authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
			.Returns(Task.FromResult(AuthorizationResult.Failed()));
		var service = new RoomAppService(null!, null!, null!, null!, null!)
		{
			LazyServiceProvider = new AbpLazyServiceProvider(new ServiceCollection()
				.AddSingleton<IAuthorizationService>(authorization).BuildServiceProvider())
		};
		Func<Task> action = operation switch
		{
			"get" => () => service.GetAsync(Guid.NewGuid()),
			"image" => () => service.GetImageAsync(Guid.NewGuid()),
			"delete" => () => service.DeleteAsync(Guid.NewGuid()),
			"list" => () => service.GetListAsync(new()),
			"features" => () => service.GetFeaturesAsync(),
			"create" => () => service.CreateAsync(new()),
			"update" => () => service.UpdateAsync(Guid.NewGuid(), new()),
			_ => () => service.CreateFeatureAsync(new() { Name = "Smart Board" })
		};
		(await Should.ThrowAsync<AbpAuthorizationException>(action)).Data["PolicyName"].ShouldBe(denied);
	}
}

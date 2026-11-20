using System;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Notifications;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryNotificationRecipientAccess_Tests
{
	[Fact]
	public async Task Notification_Recipient_Search_Should_Reject_A_Removed_Database_Role_Despite_Stale_Token_Role_Claims()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var role = new IdentityRole(Guid.NewGuid(), "library-notifier-" + Guid.NewGuid().ToString("N"), context.TenantId);
		await WithUnitOfWorkAsync(async () => {
			await GetRequiredService<IRepository<IdentityRole, Guid>>().InsertAsync(role, true);
			var users = GetRequiredService<IIdentityUserRepository>();
			var user = await users.GetAsync(context.ActorId); user.AddRole(role.Id); await users.UpdateAsync(user, true);
			var permissions = GetRequiredService<IPermissionManager>();
			foreach (var permission in new[] { Send, ShikshaMitraSubmodulePermissions.Library.Notification.View })
			{
				await permissions.SetAsync(permission, "U", context.ActorId.ToString(), false);
				await permissions.SetAsync(permission, "R", role.Name, true);
			}
		});
		using var staleSession = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, context.ActorId.ToString()), new Claim(AbpClaimTypes.TenantId, context.TenantId.ToString()),
			new Claim(AbpClaimTypes.Role, role.Name)
		], "LibraryStaleRoleTest", AbpClaimTypes.UserName, AbpClaimTypes.Role)));
		(await Services().Get<IPermissionChecker>().IsGrantedAsync(Send)).ShouldBeTrue("The test must first prove its role grant is usable.");
		(await SearchRecipientAsync(context)).Items.ShouldNotBeEmpty();
		await WithUnitOfWorkAsync(async () => {
			var users = GetRequiredService<IIdentityUserRepository>();
			var user = await users.GetAsync(context.ActorId); user.RemoveRole(role.Id); await users.UpdateAsync(user, true);
		});
		GetRequiredService<ICurrentPrincipalAccessor>().Principal!.IsInRole(role.Name).ShouldBeTrue("The token still carries its old role claim after the actual membership is removed.");
		await Should.ThrowAsync<AbpAuthorizationException>(() => SearchRecipientAsync(context));
	}
}

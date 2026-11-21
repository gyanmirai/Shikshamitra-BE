using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace ShikshaMitra.Library.Notifications;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Library.Notification.View)]
[Authorize(ShikshaMitraSubmodulePermissions.Library.Notification.Add)]
public class LibraryNotificationRecipientLookupAppService(
	LibraryNotificationRecipientReader reader,
	IPermissionChecker permissions,
	IIdentityUserRepository users,
	IUserScopeAccessService scopes,
	IUnitOfWorkManager units) : ApplicationService, ILibraryNotificationRecipientLookupAppService
{
	public async Task<PaginatedResponse<LibraryNotificationRecipientOptionDto>> SearchAsync(LibraryNotificationRecipientSearchRequest input)
	{
		using var read = units.Begin(requiresNew: true, isTransactional: false);
		if (!CurrentTenant.Id.HasValue || !CurrentUser.Id.HasValue ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Notification.View) ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Notification.Add))
			throw new AbpAuthorizationException();
		var user = await users.FindAsync(CurrentUser.Id.Value);
		if (user == null || !user.IsActive || user.TenantId != CurrentTenant.Id) throw new AbpAuthorizationException();
		var scope = await scopes.GetCurrentUserScopeAsync();
		scopes.EnsureHasAnyAccess(scope, UserScopeErrorCodes.AccessDenied);
		if (scope.TenantId != CurrentTenant.Id || scope.UserId != CurrentUser.Id) throw new AbpAuthorizationException();
		var result = await reader.SearchAsync(input, scope);
		await read.CompleteAsync();
		return result;
	}
}

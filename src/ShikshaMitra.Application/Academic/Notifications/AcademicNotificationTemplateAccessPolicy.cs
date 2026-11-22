using System;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(AcademicNotificationTemplateAccessPolicy), typeof(INotificationTemplateAccessPolicy))]
public sealed class AcademicNotificationTemplateAccessPolicy(ICurrentTenant tenant, ICurrentUser current,
	IRepository<IdentityUser, Guid> users, IUserScopeAccessService scopes, IPermissionChecker permissions,
	StudentScopeAccessService studentScopes) : INotificationTemplateAccessPolicy, ITransientDependency
{
	public bool SupportsEvent(string eventKey) => !string.IsNullOrEmpty(eventKey) && eventKey.StartsWith("Academic.", StringComparison.Ordinal);
	public async Task EnsureAsync(string eventKey, Guid? academicMediumId, NotificationTemplateAction action,
		CancellationToken cancellationToken)
	{
		if (!SupportsEvent(eventKey)) throw new AbpAuthorizationException();
		var permission = action switch {
			NotificationTemplateAction.Read => ShikshaMitraSubmodulePermissions.Academics.Notification.View,
			NotificationTemplateAction.Write => ShikshaMitraSubmodulePermissions.Academics.Notification.Add,
			NotificationTemplateAction.Delete => ShikshaMitraSubmodulePermissions.Academics.Notification.Delete,
			_ => throw new AbpAuthorizationException()
		};
		var (_, _, scope) = await AcademicNotificationIdentityAccess.RequireAsync(tenant, current, users, scopes, permissions, permission, cancellationToken);
		if (academicMediumId is Guid mediumId)
		{
			if (action == NotificationTemplateAction.Read) await studentScopes.EnsureCanAccessMediumAsync(mediumId, nameof(EnsureAsync));
			else await studentScopes.EnsureCanManageMediumAsync(mediumId, nameof(EnsureAsync));
		}
		else if (action != NotificationTemplateAction.Read && !scope.HasTenantWideScope()) throw new AbpAuthorizationException();
	}
}

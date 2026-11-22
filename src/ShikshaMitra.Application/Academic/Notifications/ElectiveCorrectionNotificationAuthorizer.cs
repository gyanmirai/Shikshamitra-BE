using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(IAcademicNotificationSourceAuthorizer))]
public sealed class ElectiveCorrectionNotificationAuthorizer(ElectiveCorrectionNotificationSource source,
	AcademicNotificationRuleScopeAccess access, IUserScopeService scopes, IPermissionChecker permissions, ICurrentTenant tenant,
	ICurrentUser actor) : IAcademicNotificationSourceAuthorizer, ITransientDependency
{
	public IReadOnlyCollection<string> EventKeys => source.EventKeys;

	public async Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken token)
	{
		if (!EventKeys.Contains(eventKey) || tenant.Id is not Guid school || actor.Id is not Guid user || sourceId == Guid.Empty ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.ClassManagement.StudentElectiveAssignment.View) ||
			await source.FindAsync(school, sourceId, token) is not { } sourceRow) throw new AbpAuthorizationException();
		var scope = sourceRow.Notice.Scope;
		var caller = await scopes.GetUserScopeAsync(user);
		if (caller.TenantId != school) throw new AbpAuthorizationException();
		await access.RequireAsync(new(scope.AcademicYearId, scope.BoardId, scope.MediumId, scope.ClassId, scope.DivisionId), school, caller, token);
	}

	public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken token) =>
		EnsureCanViewAsync(eventKey, sourceId, token);
}

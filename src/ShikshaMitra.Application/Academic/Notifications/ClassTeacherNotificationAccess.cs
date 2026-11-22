using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.Permissions;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class ClassTeacherNotificationAccess(IConfiguration configuration, IRepository<IdentityUser, Guid> users,
	AdminPermissionAccess identities, IUserScopeService userScopes, IPermissionChecker permissions,
	AcademicNotificationRuleScopeAccess scopeAccess, IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal const string View = ShikshaMitraSubmodulePermissions.ClassManagement.ClassTeacherAssignment.View;

	internal async Task<NotificationRecipientRefDto?> FindReviewerAsync(Guid school, NotificationSourceScopeDto source, Guid? actor,
		CancellationToken token)
	{
		var value = configuration[$"Notifications:Academic:Tenants:{school:N}:ClassTeacherReviewerUserId"];
		if (!Guid.TryParse(value, out var userId) || userId == Guid.Empty || userId == actor ||
			!await queries.AnyAsync((await users.GetQueryableAsync()).Where(x =>
				x.Id == userId && x.TenantId == school && !x.IsDeleted && x.IsActive), token)) return null;
		var roles = await identities.GetRolesAsync(userId);
		if (roles == null) return null;

		var claims = new List<Claim>
		{
			new(AbpClaimTypes.UserId, userId.ToString()),
			new(AbpClaimTypes.TenantId, school.ToString())
		};
		claims.AddRange(roles.Select(x => new Claim(AbpClaimTypes.Role, x)));
		var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "ClassTeacherNotificationPermissions", AbpClaimTypes.UserName, AbpClaimTypes.Role));
		if (!await permissions.IsGrantedAsync(principal, View) ||
			!await permissions.IsGrantedAsync(principal, ShikshaMitraSubmodulePermissions.ClassManagement.ClassTeacherAssignment.Add)) return null;

		try { await EnsureScopeAsync(school, userId, source, token); }
		catch (AbpAuthorizationException) { return null; }
		return new("ConfiguredReviewer", UserId: userId);
	}

	internal async Task EnsureScopeAsync(Guid school, Guid userId, NotificationSourceScopeDto source, CancellationToken token)
	{
		var scope = await userScopes.GetUserScopeAsync(userId);
		if (scope.TenantId != school) throw new AbpAuthorizationException();
		await scopeAccess.RequireAsync(new(source.AcademicYearId, source.BoardId, source.MediumId, source.ClassId, source.DivisionId),
			school, scope, token);
	}
}

[RemoteService(false), ExposeServices(typeof(IAcademicNotificationSourceAuthorizer))]
public sealed class ClassTeacherNotificationSourceAuthorizer(ClassTeacherNotificationSource source, ClassTeacherNotificationAccess access,
	IPermissionChecker permissions, ICurrentTenant tenant, Volo.Abp.Users.ICurrentUser current) : IAcademicNotificationSourceAuthorizer, ITransientDependency
{
	public IReadOnlyCollection<string> EventKeys => source.EventKeys;

	public async Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken token)
	{
		if (!EventKeys.Contains(eventKey) || sourceId == Guid.Empty || tenant.Id is not Guid school || current.Id is not Guid user ||
			!await permissions.IsGrantedAsync(ClassTeacherNotificationAccess.View)) throw new AbpAuthorizationException();
		var placement = await source.FindPlacementAsync(school, sourceId, token) ?? throw new AbpAuthorizationException();
		await access.EnsureScopeAsync(school, user, placement.Scope, token);
	}

	public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken token) =>
		EnsureCanViewAsync(eventKey, sourceId, token);
}

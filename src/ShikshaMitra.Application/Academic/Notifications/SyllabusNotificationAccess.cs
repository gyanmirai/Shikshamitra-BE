using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Users;
using ChapterPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_ChapterManagement;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(SyllabusNotificationAccess), typeof(IAcademicNotificationSourceAuthorizer))]
public sealed class SyllabusNotificationAccess(SyllabusNotificationSource syllabus, SubjectAllocationNotificationSource subjects,
	AcademicNotificationRuleScopeAccess access, IUserScopeService scopes, IPermissionChecker permissions,
	ICurrentTenant tenant, ICurrentUser actor, ICancellationTokenProvider cancellation)
	: IAcademicNotificationSourceAuthorizer, ITransientDependency
{
	public IReadOnlyCollection<string> EventKeys => syllabus.EventKeys;

	internal async Task<bool> CanCaptureAsync(AssignedSubject assignment)
	{
		if (tenant.Id is not Guid school || assignment.TenantId != school || actor.Id is not Guid user ||
			!await permissions.IsGrantedAsync(ChapterPermissions.Add)) return false;
		var token = cancellation.Token;
		var source = await subjects.FindAsync(school, assignment.Id, includeDeleted: false, token);
		if (source == null) return false;
		try
		{
			await RequirePlacementAsync(school, user, source, token);
			return true;
		}
		catch (AbpAuthorizationException) { return false; }
	}

	public async Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken token)
	{
		if (tenant.Id is not Guid school || actor.Id is not Guid user || sourceId == Guid.Empty ||
			!await permissions.IsGrantedAsync(ChapterPermissions.View) || !EventKeys.Contains(eventKey) ||
			await syllabus.FindAsync(school, sourceId, token) is not { } source)
			throw new AbpAuthorizationException();
		await RequirePlacementAsync(school, user, source.Subject, token);
	}

	public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken token) => EnsureCanViewAsync(eventKey, sourceId, token);

	private async Task RequirePlacementAsync(Guid school, Guid user, SubjectNoticePlacement source, CancellationToken token)
	{
		var caller = await scopes.GetUserScopeAsync(user);
		if (caller.TenantId != school) throw new AbpAuthorizationException();
		var scope = source.Scope;
		await access.RequireAsync(new(scope.AcademicYearId, scope.BoardId, scope.MediumId, scope.ClassId, null),
			school, caller, token);
	}
}

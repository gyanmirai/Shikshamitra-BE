using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.Permissions;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class MarksNotificationAccess(ExamAppService marks, IConfiguration configuration, IRepository<IdentityUser, Guid> users,
	AdminPermissionAccess identities, IAsyncQueryableExecuter queries, IPermissionChecker permissions, ICurrentPrincipalAccessor principals)
	: ITransientDependency
{
	internal async Task<NotificationRecipientRefDto?> FindReviewerAsync(Guid school, ExamMarksWorkflowState state, CancellationToken token)
	{
		var configured = configuration[$"Notifications:Academic:Tenants:{school:N}:MarksReviewerUserId"];
		return Guid.TryParse(configured, out var reviewer) && await CanReceiveAsync(school, state, reviewer, token)
			? new("ConfiguredReviewer", UserId: reviewer) : null;
	}

	internal async Task<bool> CanReceiveAsync(Guid school, ExamMarksWorkflowState state, Guid user, CancellationToken token)
	{
		if (user == Guid.Empty || user == state.LastModifierId ||
			!await queries.AnyAsync((await users.GetQueryableAsync()).Where(x =>
				x.Id == user && x.TenantId == school && !x.IsDeleted && x.IsActive), token)) return false;
		var roles = await identities.GetRolesAsync(user);
		if (roles == null) return false;
		var claims = new List<Claim>
		{
			new(AbpClaimTypes.UserId, user.ToString()),
			new(AbpClaimTypes.TenantId, school.ToString())
		};
		claims.AddRange(roles.Select(role => new Claim(AbpClaimTypes.Role, role)));
		var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "MarksNotificationPermissions", AbpClaimTypes.UserName, AbpClaimTypes.Role));
		if (!await permissions.IsGrantedAsync(principal, ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.View) ||
			!await permissions.IsGrantedAsync(principal, ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.Add)) return false;

		using var current = principals.Change(principal);
		try { return await CanAccessRecordAsync(state, school); }
		catch (BusinessException) { return false; }
		catch (EntityNotFoundException) { return false; }
		catch (AbpAuthorizationException) { return false; }
	}

	internal async Task<bool> CanAccessRecordAsync(ExamMarksWorkflowState state, Guid school) =>
		(await marks.EnsureMarksWorkflowTargetAsync(state.ExamId, state.SubjectId, state.DivisionId)).TenantId == school;
}

[RemoteService(false), ExposeServices(typeof(IAcademicNotificationSourceAuthorizer))]
public sealed class MarksNotificationSourceAuthorizer(MarksNotificationSource source, MarksNotificationAccess access,
	IPermissionChecker permissions, ICurrentTenant tenant) : IAcademicNotificationSourceAuthorizer, ITransientDependency
{
	public IReadOnlyCollection<string> EventKeys => source.EventKeys;

	public async Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken token)
	{
		if (!EventKeys.Contains(eventKey) || sourceId == Guid.Empty || tenant.Id is not Guid school ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.View))
			throw new AbpAuthorizationException();
		var state = await source.FindAsync(school, sourceId, token);
		if (state == null) throw new AbpAuthorizationException();
		try
		{
			if (!await access.CanAccessRecordAsync(state, school)) throw new AbpAuthorizationException();
		}
		catch (BusinessException) { throw new AbpAuthorizationException(); }
		catch (EntityNotFoundException) { throw new AbpAuthorizationException(); }
	}

	public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken token) =>
		EnsureCanViewAsync(eventKey, sourceId, token);
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.Permissions;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.Security.Claims;
using ClassTeacherRow = ShikshaMitra.Academic.ClassTeacher.ClassTeacher;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class AcademicWeeklySummaryStaff(IConfiguration configuration, IRepository<ClassTeacherRow, Guid> assignments,
	IRepository<IdentityUser, Guid> users, AdminPermissionAccess identities, IPermissionChecker permissions,
	ClassTeacherNotificationAccess scopeAccess, TimetableNotificationAudience staff, IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal const string View = ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_AssessmentAndFeedback.View;

	internal async Task<IReadOnlyList<NotificationRecipientRefDto>> ResolveAsync(Guid school, NotificationSourceScopeDto scope, CancellationToken token)
	{
		var teacherIds = await queries.ToListAsync((await assignments.GetQueryableAsync()).Where(x =>
			x.TenantId == school && !x.IsDeleted && x.Status == ClassTeacherAssignmentStatus.Active &&
			x.AcademicMediumId == scope.MediumId && x.StandardId == scope.ClassId && x.DivisionId == scope.DivisionId)
			.Select(x => x.TeacherId).Distinct(), token);
		var candidates = (await staff.FindTeachersAsync(school, teacherIds.ToArray(), token))
			.Select(x => x with { Kind = "ClassTeacher" }).ToList();
		if (Guid.TryParse(configuration[$"Notifications:Academic:Tenants:{school:N}:WeeklySummaryReviewerUserId"], out var reviewer) &&
			reviewer != Guid.Empty)
			candidates.Add(new("ConfiguredReviewer", UserId: reviewer));

		var allowed = new HashSet<Guid>();
		foreach (var actor in candidates.Select(x => x.UserId!.Value).Distinct())
		{
			if (await CanReadAsync(school, actor, scope, token)) allowed.Add(actor);
		}
		return candidates.Where(x => allowed.Contains(x.UserId!.Value)).ToArray();
	}

	private async Task<bool> CanReadAsync(Guid school, Guid actor, NotificationSourceScopeDto scope, CancellationToken token)
	{
		var now = DateTimeOffset.UtcNow;
		var account = await queries.SingleOrDefaultAsync((await users.GetQueryableAsync()).Where(x =>
			x.Id == actor && x.TenantId == school && !x.IsDeleted && x.IsActive)
			.Select(x => new { x.LockoutEnabled, x.LockoutEnd }), token);
		if (account == null || account.LockoutEnabled && account.LockoutEnd > now) return false;
		var roles = await identities.GetRolesAsync(actor);
		if (roles == null) return false;
		var claims = new[] { new Claim(AbpClaimTypes.UserId, actor.ToString()), new Claim(AbpClaimTypes.TenantId, school.ToString()) }
			.Concat(roles.Select(role => new Claim(AbpClaimTypes.Role, role)));
		var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "WeeklySummaryPermissions", AbpClaimTypes.UserName, AbpClaimTypes.Role));
		if (!await permissions.IsGrantedAsync(principal, View)) return false;
		try { await scopeAccess.EnsureScopeAsync(school, actor, scope, token); }
		catch (AbpAuthorizationException) { return false; }
		return true;
	}
}

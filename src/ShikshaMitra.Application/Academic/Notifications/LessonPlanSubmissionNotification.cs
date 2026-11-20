using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Security.Permissions;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using static ShikshaMitra.Academic.Notifications.LearningTaskNotificationContent;
using static ShikshaMitra.Communication.Notifications.AcademicNotificationEventKeys;
using LessonPlanPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class LessonPlanSubmissionNotification(IRepository<LessonPlan, Guid> plans, IRepository<Subject, Guid> subjects,
	IRepository<IdentityUser, Guid> users, IConfiguration configuration, AdminPermissionAccess identities,
	IUserScopeService scopes, AcademicNotificationRuleScopeAccess access, IPermissionChecker permissions,
	IAbpLazyServiceProvider services, ICurrentPrincipalAccessor principals, IAsyncQueryableExecuter queries,
	ICurrentTenant tenant, ITenantStore tenants) : ITransientDependency
{
	private const string RevisionProperty = "AcademicLessonPlanSubmissionRevision";

	internal static IReadOnlyList<CurriculumNoticeCapture> Record(Guid school, IEnumerable<LessonPlan> submitted)
	{
		var notices = new List<CurriculumNoticeCapture>();
		foreach (var group in submitted.Where(plan => plan.Status == LessonPlanStatus.Submitted && plan.TenantId == school)
			.GroupBy(plan => (plan.AcademicYearId, plan.StandardId, plan.DivisionId)))
		{
			var members = group.OrderBy(plan => plan.Id).ToArray();
			var anchor = members[0];
			var revision = Guid.NewGuid().ToString("N");
			foreach (var plan in members) plan.SetProperty(RevisionProperty, revision);
			// Keep earlier batch membership when its anchor is edited and submitted again.
			anchor.SetProperty(BatchProperty(revision), JsonSerializer.Serialize(members.Select(plan => plan.Id)));
			notices.Add(new(anchor.Id, LessonPlanSubmitted, revision));
		}
		return notices;
	}

	internal async Task<NotificationSourceSnapshot> LoadAsync(LessonPlan anchor, AcademicNotificationDivision division,
		NotificationIntentContext context, CancellationToken token)
	{
		var revision = string.IsNullOrEmpty(context.SourceRevision) ? anchor.GetProperty<string>(RevisionProperty) : context.SourceRevision;
		if (revision == null || !Guid.TryParseExact(revision, "N", out _) ||
			anchor.GetProperty<string>(BatchProperty(revision)) is not { } encoded) return Unavailable();

		Guid[]? ids;
		try { ids = JsonSerializer.Deserialize<Guid[]>(encoded); }
		catch (JsonException) { return Unavailable(); }
		if (ids == null || ids.Length == 0 || ids[0] != anchor.Id) return Unavailable();

		var school = context.TenantId;
		var reviewer = await ResolveReviewerAsync(school, division.Scope, token);
		if (reviewer == null) return Unavailable();
		var scope = division.Scope;
		var submitted = await queries.ToListAsync((await plans.GetQueryableAsync()).Where(plan => plan.TenantId == school &&
			!plan.IsDeleted && Enumerable.Contains(ids, plan.Id) && plan.Status == LessonPlanStatus.Submitted &&
			plan.AcademicYearId == scope.AcademicYearId && plan.StandardId == scope.ClassId && plan.DivisionId == scope.DivisionId), token);
		var matching = new List<LessonPlan>();
		using (principals.Change(reviewer.Value.Principal))
		{
			var lessonPlans = services.LazyGetRequiredService<LessonPlanAppService>();
			foreach (var plan in submitted.Where(plan => plan.GetProperty<string>(RevisionProperty) == revision))
				if (await lessonPlans.CanReviewNotificationAsync(plan)) matching.Add(plan);
		}
		if (matching.Count == 0) return Unavailable();

		var schoolName = tenant.Name ?? (await tenants.FindAsync(school))?.Name;
		if (string.IsNullOrWhiteSpace(schoolName)) return Unavailable();
		var variables = new Dictionary<string, NotificationVariableValueDto>(StringComparer.Ordinal)
		{
			["school_name"] = new(NotificationVariableType.Text, schoolName),
			["class_name"] = new(NotificationVariableType.Text, division.Name),
			["lesson_plan_count"] = new(NotificationVariableType.Number, matching.Count.ToString(CultureInfo.InvariantCulture))
		};
		if (matching.Count == 1)
		{
			var plan = matching[0];
			var subject = await queries.SingleOrDefaultAsync((await subjects.GetQueryableAsync()).Where(x =>
				x.Id == plan.SubjectId && x.TenantId == school && !x.IsDeleted && x.EducationMediumId == scope.MediumId)
				.Select(x => x.Name), token);
			if (subject != null) variables["subject_name"] = new(NotificationVariableType.Text, subject);
			variables["lesson_plan_title"] = new(NotificationVariableType.Text, plan.Topic);
		}
		NotificationRecipientRefDto[] recipients = [new("ConfiguredReviewer", UserId: reviewer.Value.Id)];
		return new(true, null, revision, Link, variables, recipients, AudienceSlices: [new(scope, recipients)]);
	}

	internal async Task<NotificationRecipientRefDto?> FindReviewerForPlanAsync(Guid school, LessonPlan plan,
		NotificationSourceScopeDto scope, CancellationToken token)
	{
		var reviewer = await ResolveReviewerAsync(school, scope, token);
		if (reviewer == null) return null;
		using var principal = principals.Change(reviewer.Value.Principal);
		return await services.LazyGetRequiredService<LessonPlanAppService>().CanReviewNotificationAsync(plan)
			? new("ConfiguredReviewer", UserId: reviewer.Value.Id) : null;
	}

	private async Task<(Guid Id, ClaimsPrincipal Principal)?> ResolveReviewerAsync(Guid school, NotificationSourceScopeDto scope,
		CancellationToken token)
	{
		if (!Guid.TryParse(configuration[$"Notifications:Academic:Tenants:{school:N}:LessonPlanReviewerUserId"], out var reviewer) ||
			reviewer == Guid.Empty || !await queries.AnyAsync((await users.GetQueryableAsync()).Where(user =>
				user.Id == reviewer && user.TenantId == school && user.IsActive && !user.IsDeleted), token)) return null;
		var roles = await identities.GetRolesAsync(reviewer);
		if (roles == null) return null;

		var claims = new List<Claim>
		{
			new(AbpClaimTypes.UserId, reviewer.ToString()),
			new(AbpClaimTypes.TenantId, school.ToString())
		};
		claims.AddRange(roles.Select(role => new Claim(AbpClaimTypes.Role, role)));
		var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "LessonPlanNotificationPermissions",
			AbpClaimTypes.UserName, AbpClaimTypes.Role));
		if (!await permissions.IsGrantedAsync(principal, LessonPlanPermissions.View) ||
			!await permissions.IsGrantedAsync(principal, LessonPlanPermissions.Add)) return null;
		var caller = await scopes.GetUserScopeAsync(reviewer);
		if (caller.TenantId != school) return null;

		try
		{
			await access.RequireAsync(new(scope.AcademicYearId, scope.BoardId, scope.MediumId, scope.ClassId, scope.DivisionId),
				school, caller, token);
		}
		catch (AbpAuthorizationException) { return null; }
		return (reviewer, principal);
	}

	private static string BatchProperty(string revision) => $"AcademicLessonPlanSubmissionMembers:{revision}";
}

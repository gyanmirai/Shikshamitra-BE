using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using static ShikshaMitra.Academic.Notifications.LearningTaskNotificationContent;
using static ShikshaMitra.Communication.Notifications.AcademicNotificationEventKeys;
using LessonPlanPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record LessonPlanNoticeRevision(string Revision, Guid TeacherId, Guid ReviewerId, LessonPlanStatus Status);

[RemoteService(false), ExposeServices(typeof(INotificationSourceAdapter), typeof(IAcademicNotificationSourceAuthorizer))]
public sealed class LessonPlanNotificationSource(IRepository<LessonPlan, Guid> plans, IRepository<Subject, Guid> subjects,
	AcademicNotificationDivisionReader divisions, TimetableNotificationAudience staff, IUserScopeService scopes,
	AcademicNotificationRuleScopeAccess access, IPermissionChecker permissions, IAsyncQueryableExecuter queries,
	ICurrentTenant tenant, ICurrentUser actor, ITenantStore tenants) : INotificationSourceAdapter, IAcademicNotificationSourceAuthorizer, ITransientDependency
{
	internal const string Type = "Academic.LessonPlan";
	private const string RevisionProperty = "AcademicLessonPlanReviewNotice";
	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[] { LessonPlanApproved, LessonPlanChangesRequested, LessonPlanRejected });

	internal static CurriculumNoticeCapture Record(LessonPlan plan, Guid reviewer)
	{
		var key = ReviewEvent(plan.Status) ?? throw new InvalidOperationException("The lesson plan review must be saved before recording a notice.");
		var state = new LessonPlanNoticeRevision(Guid.NewGuid().ToString("N"), plan.TeacherId, reviewer, plan.Status);
		plan.SetProperty(RevisionProperty, NotificationRuntimeJson.Serialize(state, NotificationErrorCodes.SourceUnavailable));
		return new(plan.Id, key, state.Revision);
	}

	private static string? ReviewEvent(LessonPlanStatus status) => status switch
	{
		LessonPlanStatus.Approved => LessonPlanApproved,
		LessonPlanStatus.NeedsChanges => LessonPlanChangesRequested,
		LessonPlanStatus.Rejected => LessonPlanRejected,
		_ => null
	};

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken token)
	{
		var school = context.TenantId;
		if (school == Guid.Empty || tenant.Id != school || context.SourceType != Type || !EventKeys.Contains(context.EventKey)) return Unavailable();
		if (await FindAsync(school, context.SourceId, token) is not { } record ||
			(context.SourceScope != null && context.SourceScope != record.Division.Scope)) return Unavailable();
		var (plan, division) = record;
		if (ReviewEvent(plan.Status) != context.EventKey || plan.GetProperty<string>(RevisionProperty) is not { } json)
			return Unavailable();
		var source = NotificationRuntimeJson.Deserialize<LessonPlanNoticeRevision>(json, NotificationErrorCodes.SourceUnavailable);
		if (source.Status != plan.Status || source.TeacherId != plan.TeacherId || source.ReviewerId != plan.ReviewedBy ||
			(!string.IsNullOrEmpty(context.SourceRevision) && context.SourceRevision != source.Revision)) return Unavailable();

		var subject = await queries.SingleOrDefaultAsync((await subjects.GetQueryableAsync()).Where(x => x.Id == plan.SubjectId &&
			x.TenantId == school && !x.IsDeleted && x.EducationMediumId == division.Scope.MediumId).Select(x => x.Name), token);
		var schoolName = tenant.Name ?? (await tenants.FindAsync(school))?.Name;
		if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(schoolName)) return Unavailable();

		var recipients = (await staff.FindTeachersAsync(school, [plan.TeacherId], token))
			.Where(x => x.UserId != source.ReviewerId).ToArray();
		var variables = new Dictionary<string, NotificationVariableValueDto>(StringComparer.Ordinal)
		{
			["school_name"] = new(NotificationVariableType.Text, schoolName),
			["class_name"] = new(NotificationVariableType.Text, division.Name),
			["subject_name"] = new(NotificationVariableType.Text, subject),
			["lesson_plan_title"] = new(NotificationVariableType.Text, plan.Topic)
		};
		return new(true, null, source.Revision, Link, variables, recipients, AudienceSlices: [new(division.Scope, recipients)]);
	}

	public async Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken token)
	{
		if (!EventKeys.Contains(eventKey) || tenant.Id is not Guid school || actor.Id is not Guid user ||
			!await permissions.IsGrantedAsync(LessonPlanPermissions.View) ||
			await FindAsync(school, sourceId, token) is not { } record) throw new AbpAuthorizationException();
		var scope = record.Division.Scope;
		var caller = await scopes.GetUserScopeAsync(user);
		if (caller.TenantId != school) throw new AbpAuthorizationException();
		await access.RequireAsync(new(scope.AcademicYearId, scope.BoardId, scope.MediumId, scope.ClassId, scope.DivisionId), school, caller, token);
	}

	public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken token) =>
		EnsureCanViewAsync(eventKey, sourceId, token);

	private async Task<(LessonPlan Plan, AcademicNotificationDivision Division)?> FindAsync(Guid school, Guid id, CancellationToken token)
	{
		var plan = await queries.SingleOrDefaultAsync((await plans.GetQueryableAsync()).Where(x =>
			x.Id == id && !x.IsDeleted && x.TenantId == school), token);
		if (plan == null) return null;
		var division = await divisions.FindAsync(school, plan.DivisionId, token);
		return division == null || division.Scope.ClassId != plan.StandardId || division.Scope.AcademicYearId != plan.AcademicYearId
			? null : (plan, division);
	}
}

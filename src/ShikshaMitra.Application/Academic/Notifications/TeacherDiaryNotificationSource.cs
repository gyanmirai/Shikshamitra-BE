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
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using static ShikshaMitra.Academic.Notifications.LearningTaskNotificationContent;
using LessonPlanPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(INotificationSourceAdapter), typeof(IAcademicNotificationSourceAuthorizer))]
public sealed class TeacherDiaryNotificationSource(IRepository<TeacherDiary, Guid> diaries, IRepository<LessonPlan, Guid> plans,
	IRepository<Subject, Guid> subjects, AcademicNotificationDivisionReader divisions, TimetableNotificationAudience staff,
	LessonPlanSubmissionNotification reviewers, AcademicNotificationRuleScopeAccess access, IUserScopeService scopes,
	IPermissionChecker permissions, IAbpLazyServiceProvider services, IAsyncQueryableExecuter queries,
	ICurrentTenant tenant, ITenantStore tenants, ICurrentUser current)
	: INotificationSourceAdapter, IAcademicNotificationSourceAuthorizer, ITransientDependency
{
	internal const string Type = "Academic.Diary";
	internal const string EventKey = AcademicNotificationEventKeys.DiaryFollowUpRequested;
	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[] { EventKey });

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken token)
	{
		var school = context.TenantId;
		if (school == Guid.Empty || tenant.Id != school || context.EventKey != EventKey || context.SourceType != Type ||
			await FindAsync(school, context.SourceId, token) is not { } record ||
			(context.SourceScope != null && context.SourceScope != record.Division.Scope)) return Unavailable();

		var (diary, plan, division) = record;
		if (diary.Status != TeacherDiaryStatus.Submitted || !diary.SubmittedAt.HasValue ||
			plan.Status != LessonPlanStatus.Approved || diary.TeacherId != plan.TeacherId ||
			string.IsNullOrWhiteSpace(diary.ConcurrencyStamp)) return Unavailable();

		var revision = diary.ConcurrencyStamp;
		if (!string.IsNullOrEmpty(context.SourceRevision) && context.SourceRevision != revision) return Unavailable();
		var schoolName = tenant.Name ?? (await tenants.FindAsync(school))?.Name;
		if (string.IsNullOrWhiteSpace(schoolName)) return Unavailable();
		var subject = await queries.SingleOrDefaultAsync((await subjects.GetQueryableAsync()).Where(x =>
			x.TenantId == school && x.Id == plan.SubjectId && !x.IsDeleted && x.EducationMediumId == division.Scope.MediumId)
			.Select(x => x.Name), token);
		if (string.IsNullOrWhiteSpace(subject)) return Unavailable();

		var recipients = (await staff.FindTeachersAsync(school, [diary.TeacherId], token)).ToList();
		var reviewer = await reviewers.FindReviewerForPlanAsync(school, plan, division.Scope, token);
		if (reviewer != null) recipients.Add(reviewer);
		if (recipients.Count == 0) return Unavailable();

		var variables = new Dictionary<string, NotificationVariableValueDto>(StringComparer.Ordinal)
		{
			["school_name"] = new(NotificationVariableType.Text, schoolName),
			["class_name"] = new(NotificationVariableType.Text, division.Name),
			["subject_name"] = new(NotificationVariableType.Text, subject),
			["lesson_plan_title"] = new(NotificationVariableType.Text, plan.Topic)
		};
		return new(true, null, revision, Link, variables, recipients, AudienceSlices: [new(division.Scope, recipients)]);
	}

	public async Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken token)
	{
		if (eventKey != EventKey || tenant.Id is not Guid school || current.Id is not Guid user ||
			!await permissions.IsGrantedAsync(LessonPlanPermissions.View) ||
			await FindAsync(school, sourceId, token) is not { } record) throw new AbpAuthorizationException();
		var scope = record.Division.Scope;
		var caller = await scopes.GetUserScopeAsync(user);
		if (caller.TenantId != school) throw new AbpAuthorizationException();
		await access.RequireAsync(new(scope.AcademicYearId, scope.BoardId, scope.MediumId, scope.ClassId, scope.DivisionId), school, caller, token);
		try
		{
			await services.LazyGetRequiredService<LessonPlanAppService>().EnsureDiaryReadAccessAsync(record.Diary, record.Plan);
		}
		catch (BusinessException error) when (error.Code == UserScopeErrorCodes.AccessDenied) { throw new AbpAuthorizationException(); }
	}

	public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken token) =>
		EnsureCanViewAsync(eventKey, sourceId, token);

	private async Task<(TeacherDiary Diary, LessonPlan Plan, AcademicNotificationDivision Division)?> FindAsync(Guid school, Guid id,
		CancellationToken token)
	{
		var lookup = from diary in await diaries.GetQueryableAsync()
			join plan in await plans.GetQueryableAsync() on diary.LessonPlanId equals plan.Id
			where diary.Id == id && diary.TenantId == school && !diary.IsDeleted && plan.TenantId == school && !plan.IsDeleted
			select new { Diary = diary, Plan = plan };
		var saved = await queries.SingleOrDefaultAsync(lookup, token);
		if (saved == null) return null;
		var division = await divisions.FindAsync(school, saved.Plan.DivisionId, token);
		return division == null || division.Scope.AcademicYearId != saved.Plan.AcademicYearId ||
			division.Scope.ClassId != saved.Plan.StandardId ? null : (saved.Diary, saved.Plan, division);
	}
}

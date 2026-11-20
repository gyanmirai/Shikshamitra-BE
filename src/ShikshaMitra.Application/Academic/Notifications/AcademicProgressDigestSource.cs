using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Academic.ClassManagement.ChapterProgress;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Security.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Users;
using static ShikshaMitra.Academic.Notifications.LearningTaskNotificationContent;
using ChapterPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_ChapterManagement;
using ClassTeacherRecord = ShikshaMitra.Academic.ClassTeacher.ClassTeacher;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(INotificationSourceAdapter), typeof(IAcademicNotificationSourceAuthorizer))]
public sealed class AcademicProgressDigestSource(AcademicProgressDigestTargets targets, SubjectAllocationNotificationSource subjects,
	AcademicNotificationDivisionReader divisions, IRepository<Division, Guid> divisionRows,
	IRepository<TeacherSubjectAllocation, Guid> allocations, IRepository<ClassTeacherRecord, Guid> classTeachers,
	TimetableNotificationAudience staff, IRepository<IdentityUser, Guid> users, AdminPermissionAccess identities,
	IUserScopeService scopes, IPermissionChecker permissions, IConfiguration configuration, IAsyncQueryableExecuter queries,
	AcademicProgressDigestTiming timing, NotificationSchoolTimeZone schoolZone, ICurrentTenant tenant, ITenantStore tenants,
	ICurrentUser current) : INotificationSourceAdapter, IAcademicNotificationSourceAuthorizer, ITransientDependency
{
	private const string Type = AcademicProgressDigestDueSource.Type;
	private const string ProgressWorkspace = "/academic/courseSubjects";
	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[]
	{
		AcademicNotificationEventKeys.ProgressBehindSchedule, AcademicNotificationEventKeys.ProgressMilestoneReached
	});

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken token)
	{
		var school = context.TenantId;
		if (school == Guid.Empty || tenant.Id != school || !EventKeys.Contains(context.EventKey) || context.SourceType != Type ||
			context.SourceScope is not { DivisionId: Guid divisionId } scope || context.DueOccurrenceAt is not { } cutoff ||
			await divisions.FindAsync(school, divisionId, token) is not { } division || division.Scope != scope ||
			await subjects.FindAsync(school, context.SourceId, includeDeleted: false, token) is not { } subject ||
			subject.Scope != (scope with { DivisionId = null }) || subject.Assignment.Status != SubjectStatus.Published)
			return Unavailable();

		var zone = await schoolZone.GetAsync(token);
		var window = await timing.ResolveAsync(context.EventKey, scope, context.OccurredAt, zone, token);
		if (window == null || cutoff != window.Period.End || context.SourceRevision != window.Revision(scope))
			return Unavailable();

		var count = await targets.CountAsync(school, subject.Assignment.Id, divisionId, window, token);
		if (count == 0) return Unavailable();
		var schoolName = tenant.Name ?? (await tenants.FindAsync(school))?.Name;
		if (string.IsNullOrWhiteSpace(schoolName)) return Unavailable();

		var recipients = await ReadRecipientsAsync(school, subject.Assignment, scope, divisionId,
			includeReviewer: !window.MilestonePercentage.HasValue, token);
		if (recipients.Count == 0) return Unavailable();
		var variables = new Dictionary<string, NotificationVariableValueDto>(StringComparer.Ordinal)
		{
			["school_name"] = new(NotificationVariableType.Text, schoolName),
			["class_name"] = new(NotificationVariableType.Text, division.Name),
			["subject_name"] = new(NotificationVariableType.Text, subject.SubjectName),
			["chapter_count"] = new(NotificationVariableType.Number, count.ToString(CultureInfo.InvariantCulture))
		};
		if (window.MilestonePercentage is int percentage)
			variables["milestone_percentage"] = new(NotificationVariableType.Number, percentage.ToString(CultureInfo.InvariantCulture));
		return new(true, null, context.SourceRevision, ProgressWorkspace, variables, recipients, window.Period.ExpiresAt,
			AudienceSlices: [new(scope, recipients)]);
	}

	public async Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken token)
	{
		if (!EventKeys.Contains(eventKey) || tenant.Id is not Guid school || current.Id is not Guid user ||
			!await permissions.IsGrantedAsync(ChapterPermissions.View) ||
			await subjects.FindAsync(school, sourceId, includeDeleted: false, token) is not { } subject)
			throw new AbpAuthorizationException();
		var caller = await scopes.GetUserScopeAsync(user);
		if (caller.TenantId != school) throw new AbpAuthorizationException();
		var sections = await queries.ToListAsync((await divisionRows.GetQueryableAsync()).Where(x =>
			x.TenantId == school && x.StandardId == subject.Assignment.StandardId && !x.IsDeleted).Select(x => x.Id), token);
		if (!sections.Any(id => ChapterProgressAppService.CanAccessClassDivision(caller, subject.Assignment.StandardId, id)))
			throw new AbpAuthorizationException();
	}

	public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken token) =>
		throw new AbpAuthorizationException();

	private async Task<IReadOnlyList<NotificationRecipientRefDto>> ReadRecipientsAsync(Guid school, AssignedSubject assignment,
		NotificationSourceScopeDto scope, Guid divisionId, bool includeReviewer, CancellationToken token)
	{
		var sectionTeachers = await queries.ToListAsync((await allocations.GetQueryableAsync()).Where(x =>
			x.StandardId == assignment.StandardId && x.SubjectId == assignment.SubjectId && x.DivisionId == divisionId && !x.IsDeleted)
			.SelectMany(x => x.Teachers.Where(teacher => !teacher.IsDeleted).Select(teacher => teacher.TeacherId)).Distinct(), token);
		var assigned = SubjectAllocationNotification.Teachers(assignment).Select(x => x.EmployeeId)
			.Concat(sectionTeachers).Where(id => id != Guid.Empty).Distinct().ToArray();
		var recipients = (await staff.FindTeachersAsync(school, assigned, token)).ToList();

		var classTeacherIds = await queries.ToListAsync((await classTeachers.GetQueryableAsync()).Where(x =>
			x.TenantId == school && x.AcademicMediumId == scope.MediumId && x.StandardId == scope.ClassId && x.DivisionId == divisionId &&
			x.Status == ClassTeacherAssignmentStatus.Active && !x.IsDeleted).Select(x => x.TeacherId), token);
		recipients.AddRange((await staff.FindTeachersAsync(school, classTeacherIds.Distinct().ToArray(), token))
			.Select(x => x with { Kind = "ClassTeacher" }));

		if (includeReviewer && await FindReviewerAsync(school, assignment.StandardId, divisionId, token) is { } reviewer)
			recipients.Add(reviewer);
		var recipientsIds = recipients.Select(x => x.UserId!.Value).Distinct().ToArray();
		var active = await queries.ToListAsync((await users.GetQueryableAsync()).Where(x =>
			x.TenantId == school && x.IsActive && !x.IsDeleted && Enumerable.Contains(recipientsIds, x.Id))
			.Select(x => x.Id), token);
		var activeIds = active.ToHashSet();
		return recipients.Where(x => activeIds.Contains(x.UserId!.Value)).Distinct().ToArray();
	}

	private async Task<NotificationRecipientRefDto?> FindReviewerAsync(Guid school, Guid standardId, Guid divisionId, CancellationToken token)
	{
		if (!Guid.TryParse(configuration[$"Notifications:Academic:Tenants:{school:N}:ProgressReviewerUserId"], out var reviewer) ||
			reviewer == Guid.Empty || !await queries.AnyAsync((await users.GetQueryableAsync()).Where(x =>
				x.Id == reviewer && x.TenantId == school && x.IsActive && !x.IsDeleted), token)) return null;
		var roles = await identities.GetRolesAsync(reviewer);
		if (roles == null) return null;
		var claims = new[] { new Claim(AbpClaimTypes.UserId, reviewer.ToString()), new Claim(AbpClaimTypes.TenantId, school.ToString()) }
			.Concat(roles.Select(role => new Claim(AbpClaimTypes.Role, role)));
		var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "ProgressNotificationPermissions", AbpClaimTypes.UserName, AbpClaimTypes.Role));
		if (!await permissions.IsGrantedAsync(principal, ChapterPermissions.View)) return null;
		var caller = await scopes.GetUserScopeAsync(reviewer);
		return caller.TenantId == school && ChapterProgressAppService.CanAccessClassDivision(caller, standardId, divisionId)
			? new("ConfiguredReviewer", UserId: reviewer) : null;
	}
}

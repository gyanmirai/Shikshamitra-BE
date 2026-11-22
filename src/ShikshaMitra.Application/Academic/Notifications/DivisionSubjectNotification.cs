using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using static ShikshaMitra.Academic.Notifications.LearningTaskNotificationContent;
using ClassTeacherRecord = ShikshaMitra.Academic.ClassTeacher.ClassTeacher;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record DivisionSubjectNoticeState(string Revision, Guid AssignmentId, string? Publication, Guid? GroupId,
	Guid[] Current, Guid[] Added, Guid[] Removed);
internal sealed record DivisionSubjectNoticePlacement(TeacherSubjectAllocation Allocation, NotificationSourceScopeDto Scope,
	string ClassName, string SubjectName);
internal sealed record DivisionSubjectChange(TeacherSubjectAllocation Allocation, Guid[] Before);

[RemoteService(false)]
public sealed class DivisionSubjectNotification(IRepository<TeacherSubjectAllocation, Guid> allocations, IRepository<Standard, Guid> standards,
	IRepository<Division, Guid> divisions, IRepository<Subject, Guid> subjects, IRepository<AssignedSubject, Guid> assignments,
	IRepository<ClassTeacherRecord, Guid> classTeachers, TimetableNotificationAudience staff,
	AcademicNotificationRuleScopeAccess scopeAccess, IUserScopeService scopes, INotificationPersonalWriteLock locks,
	IAsyncQueryableExecuter queries, ICurrentTenant tenant, ITenantStore tenants,
	ICancellationTokenProvider cancellation) : ITransientDependency
{
	private const string NoticeProperty = "AcademicDivisionSubjectTeacherNotice";
	private const string EventKey = AcademicNotificationEventKeys.SubjectTeacherChanged;

	internal Task LockAsync(Guid classId) => tenant.Id.HasValue
		? locks.AcquireAsync([$"academic-division-subject:{classId:N}"], cancellation.Token)
		: Task.CompletedTask;

	internal static Guid[] Teachers(TeacherSubjectAllocation allocation) => allocation.Teachers
		.Where(x => !x.IsDeleted && x.TeacherId != Guid.Empty).Select(x => x.TeacherId).Distinct().Order().ToArray();

	internal async Task PrepareAndCaptureAsync(IReadOnlyList<DivisionSubjectChange> changes, Guid mediumId, Guid? actor,
		SubjectAllocationNotification writer)
	{
		if (changes.Count == 0 || tenant.Id is not Guid school) return;
		var token = cancellation.Token;
		var caller = actor is Guid user && await writer.CanRecordAsync() ? await scopes.GetUserScopeAsync(user) : null;
		var checkedDivisions = new Dictionary<Guid, bool>();
		var operations = new List<CurriculumNoticeCapture>();
		foreach (var change in changes)
		{
			var allocation = change.Allocation;
			var current = Teachers(allocation);
			if (current.SequenceEqual(change.Before)) continue;
			allocation.RemoveProperty(NoticeProperty);
			if (caller == null || caller.TenantId != school) continue;

			var placement = await FindAsync(school, allocation.Id, token);
			if (placement == null || placement.Scope.MediumId != mediumId) continue;
			if (!checkedDivisions.TryGetValue(allocation.DivisionId, out var allowed))
			{
				allowed = await HasScopeAsync(school, caller, placement.Scope, token);
				checkedDivisions[allocation.DivisionId] = allowed;
			}
			if (!allowed) continue;

			var published = await FindPublishedAsync(school, allocation, token);
			if (published == null) continue;
			var state = new DivisionSubjectNoticeState(Guid.NewGuid().ToString("N"), published.Id,
				SubjectAllocationNotification.Read(published)?.Publication, SubjectAllocationNotification.GroupId(published),
				current, current.Except(change.Before).ToArray(), change.Before.Except(current).ToArray());
			allocation.SetProperty(NoticeProperty, NotificationRuntimeJson.Serialize(state, NotificationErrorCodes.SourceUnavailable));
			operations.Add(new(allocation.Id, EventKey, state.Revision));
		}
		await writer.CaptureAsync(operations, actor);
	}

	internal async Task<DivisionSubjectNoticePlacement?> FindAsync(Guid school, Guid id, CancellationToken token)
	{
		var placement = from allocation in await allocations.GetQueryableAsync()
			join division in await divisions.GetQueryableAsync() on allocation.DivisionId equals division.Id
			join standard in await standards.GetQueryableAsync() on allocation.StandardId equals standard.Id
			join subject in await subjects.GetQueryableAsync() on allocation.SubjectId equals subject.Id
			let stage = standard.EducationMediumStage
			let medium = stage.EducationMedium
			let board = medium.EducationBoard
			where allocation.Id == id && !allocation.IsDeleted &&
				division.TenantId == school && !division.IsDeleted && division.StandardId == standard.Id &&
				standard.TenantId == school && !standard.IsDeleted && stage.TenantId == school && !stage.IsDeleted &&
				medium.TenantId == school && !medium.IsDeleted && board.TenantId == school && !board.IsDeleted &&
				board.AcademicYear.TenantId == school && !board.AcademicYear.IsDeleted &&
				subject.TenantId == school && !subject.IsDeleted && subject.EducationMediumId == medium.Id
			select new DivisionSubjectNoticePlacement(allocation, new(board.AcademicYearId, board.Id, medium.Id, standard.Id, division.Id),
				standard.Name + " - " + division.Name, subject.Name);
		return await queries.SingleOrDefaultAsync(placement, token);
	}

	internal async Task EnsureScopeAsync(Guid school, Guid actor, NotificationSourceScopeDto scope, CancellationToken token)
	{
		var current = await scopes.GetUserScopeAsync(actor);
		if (!await HasScopeAsync(school, current, scope, token)) throw new AbpAuthorizationException();
	}

	private async Task<bool> HasScopeAsync(Guid school, UserScopeContext user, NotificationSourceScopeDto scope, CancellationToken token)
	{
		if (user.TenantId != school) return false;
		try
		{
			await scopeAccess.RequireAsync(new(scope.AcademicYearId, scope.BoardId, scope.MediumId, scope.ClassId, scope.DivisionId),
				school, user, token);
			return true;
		}
		catch (AbpAuthorizationException) { return false; }
	}

	private async Task<AssignedSubject?> FindPublishedAsync(Guid school, TeacherSubjectAllocation allocation, CancellationToken token)
	{
		var matches = await queries.ToListAsync((await assignments.GetQueryableAsync()).Where(x =>
			x.TenantId == school && !x.IsDeleted && x.StandardId == allocation.StandardId && x.SubjectId == allocation.SubjectId &&
			x.Status == SubjectStatus.Published).Take(2), token);
		return matches.Count == 1 ? matches[0] : null;
	}

	internal async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken token)
	{
		var school = context.TenantId;
		if (school != tenant.Id || context.EventKey != EventKey) return Unavailable();
		var placement = await FindAsync(school, context.SourceId, token);
		if (placement == null || (context.SourceScope != null && context.SourceScope != placement.Scope) ||
			placement.Allocation.GetProperty<string>(NoticeProperty) is not { } saved) return Unavailable();

		var state = NotificationRuntimeJson.Deserialize<DivisionSubjectNoticeState>(saved, NotificationErrorCodes.SourceUnavailable);
		if ((!string.IsNullOrEmpty(context.SourceRevision) && state.Revision != context.SourceRevision) ||
			!state.Current.SequenceEqual(Teachers(placement.Allocation))) return Unavailable();

		var published = await FindPublishedAsync(school, placement.Allocation, token);
		if (published == null || published.Id != state.AssignmentId || SubjectAllocationNotification.Read(published)?.Publication != state.Publication ||
			SubjectAllocationNotification.GroupId(published) != state.GroupId) return Unavailable();
		var schoolName = tenant.Name ?? (await tenants.FindAsync(school))?.Name;
		if (string.IsNullOrWhiteSpace(schoolName)) return Unavailable();

		var recipients = (await staff.FindTeachersAsync(school, state.Added, token)).ToList();
		var former = (await staff.FindTeachersAsync(school, state.Removed, token)).Select(x => x with { Kind = "PreviousTeacher" }).ToArray();
		recipients.AddRange(former);
		var classTeacherIds = await queries.ToListAsync((await classTeachers.GetQueryableAsync()).Where(x =>
			x.TenantId == school && !x.IsDeleted && x.StandardId == placement.Scope.ClassId && x.DivisionId == placement.Scope.DivisionId &&
			x.AcademicMediumId == placement.Scope.MediumId && x.Status == ClassTeacherAssignmentStatus.Active)
			.Select(x => x.TeacherId).Distinct(), token);
		recipients.AddRange((await staff.FindTeachersAsync(school, classTeacherIds.ToArray(), token)).Select(x => x with { Kind = "ClassTeacher" }));

		var variables = new Dictionary<string, NotificationVariableValueDto>(StringComparer.Ordinal)
		{
			["school_name"] = new(NotificationVariableType.Text, schoolName),
			["class_name"] = new(NotificationVariableType.Text, placement.ClassName),
			["subject_name"] = new(NotificationVariableType.Text, placement.SubjectName)
		};
		var content = former.Select(x => new NotificationSourceRecipientContentDto(x,
			new Dictionary<string, NotificationVariableValueDto> { ["school_name"] = variables["school_name"] }, Link)).ToArray();
		return new(true, null, state.Revision, Link, variables, recipients, AudienceSlices: [new(placement.Scope, recipients)], RecipientContent: content);
	}
}

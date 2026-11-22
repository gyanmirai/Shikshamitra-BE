using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using static ShikshaMitra.Academic.Notifications.LearningTaskNotificationContent;
using ClassTeacherRecord = ShikshaMitra.Academic.ClassTeacher.ClassTeacher;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record ElectiveCorrectionNotice(Guid SelectionId, string SelectionStamp, NotificationSourceScopeDto Scope);
internal sealed record ElectiveCorrectionState(Guid SelectedSubjectId, ElectiveCorrectionNotice? Notice = null);
internal sealed record ElectiveCorrectionPlacement(StudentElectiveCorrection Audit, ElectiveCorrectionNotice Notice,
	Guid PreviousSubjectId, Guid SelectedSubjectId, string ClassName);

[RemoteService(false), ExposeServices(typeof(ElectiveCorrectionNotificationSource), typeof(INotificationSourceAdapter))]
public sealed class ElectiveCorrectionNotificationSource(IRepository<StudentElectiveCorrection, Guid> corrections,
	IRepository<StudentElectiveSelection, Guid> selections, IRepository<ClassTeacherRecord, Guid> classTeachers,
	ElectiveNotificationAudience audience, TimetableNotificationAudience staff,
	IAsyncQueryableExecuter queries, ICurrentTenant tenant, ITenantStore tenants) : INotificationSourceAdapter, ITransientDependency
{
	internal const string Type = "Academic.Elective";
	internal const string EventKey = AcademicNotificationEventKeys.ElectiveLockedSelectionCorrected;
	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[] { EventKey });

	internal async Task<ElectiveCorrectionPlacement?> FindAsync(Guid school, Guid id, CancellationToken token)
	{
		var audit = await queries.SingleOrDefaultAsync((await corrections.GetQueryableAsync()).Where(x => x.TenantId == school && x.Id == id), token);
		if (audit == null) return null;
		ElectiveCorrectionState? before;
		ElectiveCorrectionState? after;
		try
		{
			before = JsonSerializer.Deserialize<ElectiveCorrectionState>(audit.BeforeJson);
			after = JsonSerializer.Deserialize<ElectiveCorrectionState>(audit.AfterJson);
		}
		catch (JsonException) { return null; }
		if (before == null || after?.Notice is not { } notice || notice.Scope == null ||
			notice.SelectionId == Guid.Empty || string.IsNullOrWhiteSpace(notice.SelectionStamp) ||
			before.SelectedSubjectId == Guid.Empty || after.SelectedSubjectId == Guid.Empty) return null;

		if (notice.Scope.DivisionId is not Guid divisionId ||
			await audience.FindDivisionAsync(school, divisionId, token) is not { } division || division.Scope != notice.Scope) return null;
		return new(audit, notice, before.SelectedSubjectId, after.SelectedSubjectId, division.Name);
	}

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken token)
	{
		var school = context.TenantId;
		if (school == Guid.Empty || school != tenant.Id || context.SourceId == Guid.Empty ||
			context.EventKey != EventKey || context.SourceType != Type) return Unavailable();
		var placement = await FindAsync(school, context.SourceId, token);
		if (placement == null || (context.SourceScope != null && context.SourceScope != placement.Notice.Scope)) return Unavailable();
		var revision = placement.Audit.Id.ToString("N");
		if (!string.IsNullOrEmpty(context.SourceRevision) && context.SourceRevision != revision) return Unavailable();

		var (audit, notice, previous, selected, className) = placement;
		var scope = notice.Scope;
		var group = await audience.FindGroupAsync(school, audit.ElectiveGroupId, scope.MediumId, token);
		if (group == null || !group.Subjects.Any(x => x.SubjectId == selected)) return Unavailable();

		var current = (await selections.GetQueryableAsync()).Where(x => x.TenantId == school && !x.IsDeleted &&
			x.StudentId == audit.StudentId && x.ElectiveGroupId == group.Id && x.AcademicMediumId == scope.MediumId);
		var options = await queries.ToListAsync(current.Select(x => new
		{
			x.Id, x.IsLocked, x.SelectedSubjectId, x.ConcurrencyStamp
		}).Take(2), token);
		if (options.Count != 1 || options[0].Id != notice.SelectionId || !options[0].IsLocked ||
			options[0].SelectedSubjectId != selected || options[0].ConcurrencyStamp != notice.SelectionStamp) return Unavailable();

		if (!await audience.IsEnrolledAsync(school, audit.StudentId, scope, token)) return Unavailable();

		var target = await audience.FindPublishedAsync(school, group.Id, selected, scope, token);
		if (target == null) return Unavailable();
		var schoolName = tenant.Name ?? (await tenants.FindAsync(school))?.Name;
		if (string.IsNullOrWhiteSpace(schoolName)) return Unavailable();

		var subjectChanged = previous != selected;
		var prior = subjectChanged && group.Subjects.Any(x => x.SubjectId == previous)
			? await audience.FindPublishedAsync(school, group.Id, previous, scope, token) : null;
		var assigned = subjectChanged ? await audience.FindTeachersAsync(school, target.Value.Assignment, scope, token) : [];
		var previousTeachers = prior.HasValue ? await audience.FindTeachersAsync(school, prior.Value.Assignment, scope, token) : [];
		var former = previousTeachers.Where(x => assigned.All(y => y.UserId != x.UserId))
			.Select(x => x with { Kind = "PreviousTeacher" }).ToArray();
		var classTeacherIds = await queries.ToListAsync((await classTeachers.GetQueryableAsync()).Where(x => x.TenantId == school &&
			!x.IsDeleted && x.StandardId == scope.ClassId && x.DivisionId == scope.DivisionId &&
			x.AcademicMediumId == scope.MediumId && x.Status == ClassTeacherAssignmentStatus.Active)
			.Select(x => x.TeacherId).Distinct(), token);
		var classRecipients = (await staff.FindTeachersAsync(school, classTeacherIds.ToArray(), token))
			.Select(x => x with { Kind = "ClassTeacher" });
		var recipients = new[] { new NotificationRecipientRefDto("Student", StudentId: audit.StudentId) }
			.Concat(assigned).Concat(former).Concat(classRecipients).ToArray();
		var variables = ElectiveNotificationAudience.Variables(schoolName, className, target.Value.Name, group.Name);
		var outgoing = former.Select(x => new NotificationSourceRecipientContentDto(x,
			new Dictionary<string, NotificationVariableValueDto> { ["school_name"] = variables["school_name"] }, Link)).ToArray();
		return new(true, null, revision, Link, variables, recipients, AudienceSlices: [new(scope, recipients)], RecipientContent: outgoing);
	}
}

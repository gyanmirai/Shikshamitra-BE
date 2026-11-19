using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
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
	IRepository<StudentElectiveSelection, Guid> selections, IRepository<ElectiveGroup, Guid> groups,
	IRepository<AssignedSubject, Guid> assignments, IRepository<Subject, Guid> subjects,
	IRepository<TeacherSubjectAllocation, Guid> divisionAssignments, IRepository<ClassTeacherRecord, Guid> classTeachers,
	IRepository<Student, Guid> students, IRepository<Division, Guid> divisions, IRepository<Standard, Guid> standards,
	AcademicNotificationEnrollmentRoster roster, TimetableNotificationAudience staff,
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

		var scope = notice.Scope;
		var placement = from division in await divisions.GetQueryableAsync()
			join standard in await standards.GetQueryableAsync() on division.StandardId equals standard.Id
			let stage = standard.EducationMediumStage
			let medium = stage.EducationMedium
			let board = medium.EducationBoard
			where division.Id == scope.DivisionId && division.TenantId == school && !division.IsDeleted &&
				standard.Id == scope.ClassId && standard.TenantId == school && !standard.IsDeleted &&
				stage.TenantId == school && !stage.IsDeleted && medium.Id == scope.MediumId && medium.TenantId == school && !medium.IsDeleted &&
				board.Id == scope.BoardId && board.TenantId == school && !board.IsDeleted && board.AcademicYearId == scope.AcademicYearId &&
				board.AcademicYear.TenantId == school && !board.AcademicYear.IsDeleted
			select standard.Name + " - " + division.Name;
		var name = await queries.SingleOrDefaultAsync(placement, token);
		return name == null ? null : new(audit, notice, before.SelectedSubjectId, after.SelectedSubjectId, name);
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
		var group = await queries.SingleOrDefaultAsync((await groups.WithDetailsAsync(x => x.Subjects)).Where(x =>
			x.TenantId == school && x.Id == audit.ElectiveGroupId && !x.IsDeleted &&
			x.AcademicMediumId == scope.MediumId && x.Status == SubjectMasterStatus.Active), token);
		if (group == null || !group.Subjects.Any(x => x.SubjectId == selected)) return Unavailable();

		var current = (await selections.GetQueryableAsync()).Where(x => x.TenantId == school && !x.IsDeleted &&
			x.StudentId == audit.StudentId && x.ElectiveGroupId == group.Id && x.AcademicMediumId == scope.MediumId);
		var options = await queries.ToListAsync(current.Select(x => new
		{
			x.Id, x.IsLocked, x.SelectedSubjectId, x.ConcurrencyStamp
		}).Take(2), token);
		if (options.Count != 1 || options[0].Id != notice.SelectionId || !options[0].IsLocked ||
			options[0].SelectedSubjectId != selected || options[0].ConcurrencyStamp != notice.SelectionStamp) return Unavailable();

		var eligible = (await students.GetQueryableAsync()).Where(x => x.TenantId == school && !x.IsDeleted &&
			x.Id == audit.StudentId && x.Status == StudentLifeCycleStatusEnum.Enrolled);
		var learners = await roster.FindAsync(school, scope.AcademicYearId!.Value, scope.MediumId!.Value, scope.ClassId!.Value,
			[scope.DivisionId!.Value], eligible, token);
		if (learners?.Count != 1) return Unavailable();

		var target = await FindPublishedAsync(school, group.Id, selected, scope, token);
		if (target == null) return Unavailable();
		var schoolName = tenant.Name ?? (await tenants.FindAsync(school))?.Name;
		if (string.IsNullOrWhiteSpace(schoolName)) return Unavailable();

		var subjectChanged = previous != selected;
		var prior = subjectChanged && group.Subjects.Any(x => x.SubjectId == previous)
			? await FindPublishedAsync(school, group.Id, previous, scope, token) : null;
		var assigned = subjectChanged ? await FindTeachersAsync(school, target.Value.Assignment, scope, token) : [];
		var previousTeachers = prior.HasValue ? await FindTeachersAsync(school, prior.Value.Assignment, scope, token) : [];
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
		var variables = new Dictionary<string, NotificationVariableValueDto>(StringComparer.Ordinal)
		{
			["school_name"] = new(NotificationVariableType.Text, schoolName),
			["class_name"] = new(NotificationVariableType.Text, className),
			["subject_name"] = new(NotificationVariableType.Text, target.Value.Name),
			["group_name"] = new(NotificationVariableType.Text, group.Name)
		};
		var outgoing = former.Select(x => new NotificationSourceRecipientContentDto(x,
			new Dictionary<string, NotificationVariableValueDto> { ["school_name"] = variables["school_name"] }, Link)).ToArray();
		return new(true, null, revision, Link, variables, recipients, AudienceSlices: [new(scope, recipients)], RecipientContent: outgoing);
	}

	private async Task<(AssignedSubject Assignment, string Name)?> FindPublishedAsync(Guid school, Guid groupId, Guid subjectId,
		NotificationSourceScopeDto scope, CancellationToken token)
	{
		var source = from assignment in await assignments.WithDetailsAsync(x => x.Teachers)
			join subject in await subjects.GetQueryableAsync() on assignment.SubjectId equals subject.Id
			where assignment.TenantId == school && !assignment.IsDeleted && assignment.StandardId == scope.ClassId &&
				assignment.Status == SubjectStatus.Published && assignment.SubjectType == SubjectType.Elective &&
				assignment.ElectiveGroupId == groupId && subject.Id == subjectId && subject.TenantId == school && !subject.IsDeleted &&
				subject.EducationMediumId == scope.MediumId && subject.Status == SubjectMasterStatus.Active
			select new { Assignment = assignment, subject.Name };
		var rows = await queries.ToListAsync(source.Take(2), token);
		return rows.Count == 1 ? (rows[0].Assignment, rows[0].Name) : null;
	}

	private async Task<IReadOnlyList<NotificationRecipientRefDto>> FindTeachersAsync(Guid school, AssignedSubject assignment,
		NotificationSourceScopeDto scope, CancellationToken token)
	{
		var divisionsForSubject = await queries.ToListAsync((await divisionAssignments.GetQueryableAsync()).Where(x => !x.IsDeleted &&
			x.StandardId == scope.ClassId && x.DivisionId == scope.DivisionId && x.SubjectId == assignment.SubjectId), token);
		var divisionTeachers = divisionsForSubject.SelectMany(DivisionSubjectNotification.Teachers).ToArray();
		var actual = divisionTeachers.Length == 0
			? SubjectAllocationNotification.Teachers(assignment).Select(x => x.EmployeeId).Distinct().ToArray()
			: divisionTeachers.Distinct().ToArray();
		return await staff.FindTeachersAsync(school, actual, token);
	}
}

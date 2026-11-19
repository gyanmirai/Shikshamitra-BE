using System;
using System.Collections.Generic;
using System.Linq;
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
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using static ShikshaMitra.Academic.Notifications.LearningTaskNotificationContent;
using static ShikshaMitra.Communication.Notifications.AcademicNotificationEventKeys;
using ClassTeacherRecord = ShikshaMitra.Academic.ClassTeacher.ClassTeacher;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record SubjectNoticePlacement(AssignedSubject Assignment, NotificationSourceScopeDto Scope, string ClassName, string SubjectName);

[RemoteService(false), ExposeServices(typeof(SubjectAllocationNotificationSource), typeof(INotificationSourceAdapter))]
public sealed class SubjectAllocationNotificationSource(IRepository<AssignedSubject, Guid> assignments,
	IRepository<Standard, Guid> standards, IRepository<Division, Guid> divisions, IRepository<Subject, Guid> subjects,
	IRepository<ClassTeacherRecord, Guid> classTeachers, IRepository<Student, Guid> students,
	IRepository<StudentElectiveSelection, Guid> electives, IRepository<ElectiveGroup, Guid> electiveGroups,
	AcademicNotificationEnrollmentRoster enrollment,
	TimetableNotificationAudience staff, DivisionSubjectNotification divisionNotices, IDataFilter filters, IAsyncQueryableExecuter queries, ICurrentTenant tenant, ITenantStore tenants)
	: INotificationSourceAdapter, ITransientDependency
{
	internal const string Type = "Academic.Subject";
	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[] { SubjectAllocated, SubjectTeacherChanged, SubjectWithdrawn });

	internal async Task<SubjectNoticePlacement?> FindAsync(Guid school, Guid id, bool includeDeleted, CancellationToken token)
	{
		using var softDelete = includeDeleted ? filters.Disable<ISoftDelete>() : null;
		var query = from assignment in await assignments.WithDetailsAsync(x => x.Teachers)
			join subject in await subjects.GetQueryableAsync() on assignment.SubjectId equals subject.Id
			join standard in await standards.GetQueryableAsync() on assignment.StandardId equals standard.Id
			let stage = standard.EducationMediumStage
			let medium = stage.EducationMedium
			let board = medium.EducationBoard
			where assignment.Id == id && assignment.TenantId == school && (includeDeleted || !assignment.IsDeleted) &&
				subject.TenantId == school && !subject.IsDeleted && subject.EducationMediumId == medium.Id &&
				standard.TenantId == school && !standard.IsDeleted && stage.TenantId == school && !stage.IsDeleted &&
				medium.TenantId == school && !medium.IsDeleted && board.TenantId == school && !board.IsDeleted &&
				board.AcademicYear.TenantId == school && !board.AcademicYear.IsDeleted
			select new SubjectNoticePlacement(assignment, new(board.AcademicYearId, board.Id, medium.Id, standard.Id, null),
				standard.Name, subject.Name);
		return await queries.SingleOrDefaultAsync(query, token);
	}

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken token)
	{
		var school = context.TenantId;
		if (school == Guid.Empty || school != tenant.Id || context.SourceId == Guid.Empty ||
			context.SourceType != Type || !EventKeys.Contains(context.EventKey)) return Unavailable();
		var placement = await FindAsync(school, context.SourceId, context.EventKey == SubjectWithdrawn, token);
		if (placement == null)
			return context.EventKey == SubjectTeacherChanged ? await divisionNotices.LoadAsync(context, token) : Unavailable();
		var assignment = placement.Assignment;
		var state = SubjectAllocationNotification.Read(assignment);
		if (state == null || state.ClassId != assignment.StandardId || state.ElectiveGroupId != SubjectAllocationNotification.GroupId(assignment)) return Unavailable();

		var current = SubjectAllocationNotification.Teachers(assignment);
		var published = !assignment.IsDeleted && assignment.Status == SubjectStatus.Published;
		var revision = context.EventKey switch
		{
			SubjectAllocated when published => state.Publication,
			SubjectTeacherChanged when published && state.Change is { } change && change.Current.SequenceEqual(current) => change.Revision,
			SubjectWithdrawn when !published && state.Withdrawal is { } withdrawal && withdrawal.Status == assignment.Status &&
				withdrawal.IsDeleted == assignment.IsDeleted => withdrawal.Revision,
			_ => null
		};
		if (revision == null || (!string.IsNullOrEmpty(context.SourceRevision) && revision != context.SourceRevision)) return Unavailable();
		var schoolName = tenant.Name ?? (await tenants.FindAsync(school))?.Name;
		if (string.IsNullOrWhiteSpace(schoolName)) return Unavailable() with { SourceRevision = revision };

		var selected = context.EventKey switch
		{
			SubjectTeacherChanged => state.Change!.Added,
			SubjectWithdrawn => state.Withdrawal!.Teachers,
			_ => current.Select(x => x.EmployeeId).Distinct().ToArray()
		};
		var previous = context.EventKey == SubjectTeacherChanged ? state.Change!.Removed : [];
		var currentTeachers = await staff.FindTeachersAsync(school, selected, token);
		var formerTeachers = (await staff.FindTeachersAsync(school, previous, token)).Select(x => x with { Kind = "PreviousTeacher" }).ToArray();
		var slices = new List<NotificationSourceAudienceSliceDto> { new(placement.Scope, currentTeachers.Concat(formerTeachers).ToArray()) };
		var sections = await ReadSectionsAsync(placement, context, token);
		slices.AddRange(sections);
		var variables = new Dictionary<string, NotificationVariableValueDto>(StringComparer.Ordinal)
		{
			["school_name"] = new(NotificationVariableType.Text, schoolName),
			["class_name"] = new(NotificationVariableType.Text, placement.ClassName),
			["subject_name"] = new(NotificationVariableType.Text, placement.SubjectName)
		};
		var former = formerTeachers.Select(x => new NotificationSourceRecipientContentDto(x,
			new Dictionary<string, NotificationVariableValueDto> { ["school_name"] = variables["school_name"] }, Link)).ToArray();
		return new(true, null, revision, Link, variables, slices.SelectMany(x => x.Recipients).Distinct().ToArray(),
			AudienceSlices: slices, RecipientContent: former);
	}

	private async Task<IReadOnlyList<NotificationSourceAudienceSliceDto>> ReadSectionsAsync(SubjectNoticePlacement placement,
		NotificationIntentContext context, CancellationToken token)
	{
		var school = context.TenantId;
		var scope = placement.Scope;
		var requestedDivision = context.SourceScope?.DivisionId;
		if (context.SourceScope != null && !requestedDivision.HasValue) return [];
		var divisionIds = await queries.ToListAsync((await divisions.GetQueryableAsync()).Where(x =>
			x.TenantId == school && !x.IsDeleted && x.StandardId == scope.ClassId &&
			(!requestedDivision.HasValue || requestedDivision == x.Id))
			.Select(x => x.Id), token);
		if (divisionIds.Count == 0) return [];
		var activeDivisions = divisionIds.ToArray();
		var records = await queries.ToListAsync((await classTeachers.GetQueryableAsync()).Where(x => x.TenantId == school &&
			!x.IsDeleted && x.StandardId == scope.ClassId && x.AcademicMediumId == scope.MediumId &&
			Enumerable.Contains(activeDivisions, x.DivisionId) && x.Status == ClassTeacherAssignmentStatus.Active)
			.Select(x => new { x.DivisionId, x.TeacherId }), token);
		var teachers = await staff.FindTeachersAsync(school, records.Select(x => x.TeacherId).Distinct().ToArray(), token);
		var sections = records.ToLookup(x => x.DivisionId, x => x.TeacherId);
		var learners = context.EventKey == SubjectWithdrawn ? await ReadLearnersAsync(placement, school, activeDivisions, token) : [];
		var classMembers = learners.ToLookup(x => x.DivisionId, x => x.StudentId);
		return activeDivisions.Select(divisionId =>
		{
			var recipients = teachers.Where(x => sections[divisionId].Contains(x.EmployeeId!.Value) || sections[divisionId].Contains(x.UserId!.Value))
				.Select(x => x with { Kind = "ClassTeacher" })
				.Concat(classMembers[divisionId].Select(id => new NotificationRecipientRefDto("Student", StudentId: id))).ToArray();
			return new NotificationSourceAudienceSliceDto(scope with { DivisionId = divisionId }, recipients);
		}).Where(x => x.Recipients.Count > 0).ToArray();
	}

	internal async Task<IReadOnlyList<AcademicNotificationLearner>> ReadLearnersAsync(SubjectNoticePlacement placement, Guid school,
		Guid[] divisionIds, CancellationToken token)
	{
		var scope = placement.Scope;
		var active = (await students.GetQueryableAsync()).Where(x => x.TenantId == school && !x.IsDeleted &&
			x.Status == StudentLifeCycleStatusEnum.Enrolled);
		var assignment = placement.Assignment;
		if (SubjectAllocationNotification.GroupId(assignment) is Guid groupId)
		{
			if (!await queries.AnyAsync((await electiveGroups.GetQueryableAsync()).Where(x => x.Id == groupId &&
				x.TenantId == school && !x.IsDeleted && x.AcademicMediumId == scope.MediumId &&
				x.Subjects.Any(member => member.SubjectId == assignment.SubjectId)), token)) return [];
			var choices = (await electives.GetQueryableAsync()).Where(x => x.TenantId == school && !x.IsDeleted &&
				x.AcademicMediumId == scope.MediumId && x.ElectiveGroupId == groupId);
			active = active.Where(student => choices.Count(x => x.StudentId == student.Id) == 1 &&
				choices.Any(x => x.StudentId == student.Id && x.IsLocked && x.SelectedSubjectId == assignment.SubjectId));
		}
		return await enrollment.FindAsync(school, scope.AcademicYearId!.Value, scope.MediumId!.Value, scope.ClassId!.Value,
			divisionIds, active, token) ?? [];
	}
}

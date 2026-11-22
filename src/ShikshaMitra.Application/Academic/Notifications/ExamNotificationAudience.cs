using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class ExamNotificationAudience(IRepository<Student, Guid> students, AcademicNotificationEnrollmentRoster roster,
	IRepository<AssignedSubject, Guid> curriculum, IRepository<Subject, Guid> subjects, IRepository<StudentElectiveSelection, Guid> selections,
	IRepository<ElectiveGroup, Guid> groups, IRepository<TeacherSubjectAllocation, Guid> allocations,
	TimetableNotificationAudience staff, IAsyncQueryableExecuter queries)
	: ITransientDependency
{
	internal async Task<IReadOnlyDictionary<NotificationRecipientRefDto, Guid[]>> ReadAsync(Guid tenantId, ExamNotificationDivision section,
		Guid[] subjectsInWindow, CancellationToken token)
	{
		var recipients = new Dictionary<NotificationRecipientRefDto, Guid[]>();
		var assignments = await ReadPublishedAssignmentsAsync(tenantId, section, subjectsInWindow);
		var published = await queries.ToListAsync(assignments.Select(x => new { x.SubjectId, x.ElectiveGroupId }), token);
		if (published.Count == 0) return recipients;
		var active = (await students.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
			x.Status == StudentLifeCycleStatusEnum.Enrolled);
		var enrolled = await roster.FindAsync(tenantId, section.YearId, section.MediumId, section.ClassId, [section.DivisionId], active, token);
		if (enrolled == null) return recipients;
		var regular = published.Where(x => !x.ElectiveGroupId.HasValue).Select(x => x.SubjectId).Distinct().ToArray();
		var groupIds = published.Select(x => x.ElectiveGroupId).OfType<Guid>().Distinct().ToArray();
		var learnerIds = enrolled.Select(x => x.StudentId).ToArray();
		var choices = learnerIds.Length == 0 || groupIds.Length == 0 ? [] :
			await queries.ToListAsync((await selections.GetQueryableAsync()).Where(x =>
				x.TenantId == tenantId && !x.IsDeleted && x.AcademicMediumId == section.MediumId &&
				Enumerable.Contains(learnerIds, x.StudentId) && Enumerable.Contains(groupIds, x.ElectiveGroupId))
				.Select(x => new { x.StudentId, x.ElectiveGroupId, x.SelectedSubjectId, x.IsLocked }), token);
		var eligibleChoices = published.Where(x => x.ElectiveGroupId.HasValue)
			.Select(x => (Group: x.ElectiveGroupId!.Value, x.SubjectId)).ToHashSet();
		var selected = choices.GroupBy(x => (x.StudentId, x.ElectiveGroupId))
			.Where(x => x.Count() == 1 && x.First().IsLocked && eligibleChoices.Contains((x.Key.ElectiveGroupId, x.First().SelectedSubjectId)))
			.ToLookup(x => x.Key.StudentId, x => x.First().SelectedSubjectId);
		foreach (var learner in enrolled)
		{
			var applicable = regular.Concat(selected[learner.StudentId]).Distinct().ToArray();
			if (applicable.Length > 0)
				recipients.Add(new("Student", StudentId: learner.StudentId), applicable);
		}

		var subjectIds = published.Select(x => x.SubjectId).Distinct().ToArray();
		var teachers = await ReadAssignedTeachersAsync(tenantId, section, assignments, subjectIds, token);
		if (teachers == null) return new Dictionary<NotificationRecipientRefDto, Guid[]>();
		foreach (var (teacher, taughtSubjects) in teachers) recipients.Add(teacher, taughtSubjects);
		return recipients;
	}

	internal async Task<NotificationRecipientRefDto[]> ReadAssignedTeachersAsync(Guid tenantId, ExamNotificationDivision section,
		Guid subjectId, CancellationToken token)
	{
		var assignments = await ReadPublishedAssignmentsAsync(tenantId, section, [subjectId]);
		if (!await queries.AnyAsync(assignments, token)) return [];
		var teachers = await ReadAssignedTeachersAsync(tenantId, section, assignments, [subjectId], token);
		return teachers?.Keys.ToArray() ?? [];
	}

	private async Task<IQueryable<AssignedSubject>> ReadPublishedAssignmentsAsync(Guid tenantId, ExamNotificationDivision section, Guid[] subjectsInWindow)
	{
		var groupQuery = await groups.GetQueryableAsync();
		return from assignment in await curriculum.GetQueryableAsync()
			join subject in await subjects.GetQueryableAsync() on assignment.SubjectId equals subject.Id
			where assignment.TenantId == tenantId && !assignment.IsDeleted && assignment.StandardId == section.ClassId &&
				assignment.Status == SubjectStatus.Published && subject.TenantId == tenantId && !subject.IsDeleted &&
				subject.EducationMediumId == section.MediumId && subject.Status == SubjectMasterStatus.Active &&
				Enumerable.Contains(subjectsInWindow, assignment.SubjectId) &&
				(!assignment.ElectiveGroupId.HasValue || groupQuery.Any(elective => elective.Id == assignment.ElectiveGroupId &&
					elective.TenantId == tenantId && !elective.IsDeleted && elective.AcademicMediumId == section.MediumId &&
					elective.Status == SubjectMasterStatus.Active && elective.Subjects.Any(member => member.SubjectId == assignment.SubjectId)))
			select assignment;
	}

	private async Task<IReadOnlyDictionary<NotificationRecipientRefDto, Guid[]>?> ReadAssignedTeachersAsync(Guid tenantId,
		ExamNotificationDivision section, IQueryable<AssignedSubject> assignments, Guid[] subjectIds, CancellationToken token)
	{
		var classTeachers = from assignment in assignments
			from teacher in assignment.Teachers
			where teacher.TenantId == tenantId
			select new { assignment.SubjectId, teacher.TeacherId };
		var sectionTeachers = from allocation in await allocations.GetQueryableAsync()
			from teacher in allocation.Teachers
			where !allocation.IsDeleted && !teacher.IsDeleted && allocation.StandardId == section.ClassId &&
				allocation.DivisionId == section.DivisionId && Enumerable.Contains(subjectIds, allocation.SubjectId)
			select new { allocation.SubjectId, teacher.TeacherId };
		var teaching = await queries.ToListAsync(classTeachers.Union(sectionTeachers).Take(10001), token);
		if (teaching.Count > 10000) return null;
		var teachers = await staff.FindTeachersAsync(tenantId, teaching.Select(x => x.TeacherId).Distinct().ToArray(), token);
		var taughtBy = teaching.ToLookup(x => x.TeacherId, x => x.SubjectId);
		return teachers.ToDictionary(teacher => teacher, teacher =>
			taughtBy[teacher.EmployeeId!.Value].Concat(taughtBy[teacher.UserId!.Value]).Distinct().ToArray());
	}
}

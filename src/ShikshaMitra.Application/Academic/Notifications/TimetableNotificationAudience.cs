using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class TimetableNotificationAudience(IRepository<Student, Guid> studentRepository,
	AcademicNotificationEnrollmentRoster enrollmentRoster, IRepository<StudentElectiveSelection, Guid> electives,
	IRepository<AssignedSubject, Guid> assignments, IRepository<ElectiveGroup, Guid> groups, IRepository<Subject, Guid> subjects,
	IRepository<Employee, Guid> employees, IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal async Task<IReadOnlyList<NotificationRecipientRefDto>?> FindLearnersAsync(Guid tenantId, TimetableNotificationScope scope,
		TimetableNotificationTransition transition, CancellationToken token)
	{
		var affected = transition.AffectedChoices.ToHashSet();
		if (!transition.ClassWide && affected.Count == 0) return [];
		var eligible = (await studentRepository.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
			x.Status == StudentLifeCycleStatusEnum.Enrolled);
		var enrolled = await enrollmentRoster.FindAsync(tenantId, scope.YearId, scope.MediumId, scope.StandardId, [scope.DivisionId], eligible, token);
		if (enrolled == null) return null;

		var requiredGroups = transition.VisibleElectives.Groups;
		var previous = transition.PreviousElectives;
		var groupsToCheck = requiredGroups.Concat(previous?.Groups ?? []).Concat(affected.Select(x => x.GroupId)).Distinct().ToArray();
		if (enrolled.Count == 0 || groupsToCheck.Length == 0)
			return enrolled.Select(x => new NotificationRecipientRefDto("Student", StudentId: x.StudentId)).ToArray();

		var scheduled = transition.VisibleElectives.Choices.ToHashSet();
		if (requiredGroups.Length != 0 && transition.EventKey != AcademicNotificationEventKeys.TimetableWithdrawn)
			scheduled.IntersectWith(await PublishedOptionsAsync(tenantId, scope, requiredGroups, token));
		var former = previous?.Choices.ToHashSet();
		var studentIds = enrolled.Select(x => x.StudentId).ToArray();
		var currentSelections = await queries.ToListAsync((await electives.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
			!x.IsDeleted && x.AcademicMediumId == scope.MediumId && Enumerable.Contains(groupsToCheck, x.ElectiveGroupId) &&
			Enumerable.Contains(studentIds, x.StudentId))
			.Select(x => new { x.StudentId, x.ElectiveGroupId, x.SelectedSubjectId, x.IsLocked }), token);
		var approved = currentSelections.GroupBy(x => (x.StudentId, x.ElectiveGroupId))
			.Where(group => group.Count() == 1 && group.First().IsLocked)
			.ToLookup(group => group.Key.StudentId, group => new TimetableNotificationChoice(group.Key.ElectiveGroupId, group.First().SelectedSubjectId));
		return enrolled.Where(x =>
		{
			var choices = approved[x.StudentId];
			return (CanView(choices, requiredGroups, scheduled) || (previous != null && CanView(choices, previous.Groups, former!))) &&
				(transition.ClassWide || choices.Any(affected.Contains));
		}).Select(x => new NotificationRecipientRefDto("Student", StudentId: x.StudentId)).ToArray();
	}

	private static bool CanView(IEnumerable<TimetableNotificationChoice> selections, Guid[] required,
		HashSet<TimetableNotificationChoice> options) =>
		required.All(group => selections.Any(choice => choice.GroupId == group && options.Contains(choice)));

	private async Task<List<TimetableNotificationChoice>> PublishedOptionsAsync(Guid tenantId, TimetableNotificationScope scope,
		Guid[] requiredGroups, CancellationToken token) => await queries.ToListAsync(
			from assignment in await assignments.GetQueryableAsync()
			join electiveGroup in await groups.GetQueryableAsync() on assignment.ElectiveGroupId equals (Guid?)electiveGroup.Id
			join subject in await subjects.GetQueryableAsync() on assignment.SubjectId equals subject.Id
			where assignment.TenantId == tenantId && assignment.StandardId == scope.StandardId &&
				assignment.Status == SubjectStatus.Published && assignment.SubjectType == SubjectType.Elective &&
				electiveGroup.TenantId == tenantId && electiveGroup.AcademicMediumId == scope.MediumId && electiveGroup.Status == SubjectMasterStatus.Active &&
				subject.TenantId == tenantId && subject.EducationMediumId == scope.MediumId && subject.Status == SubjectMasterStatus.Active &&
				Enumerable.Contains(requiredGroups, electiveGroup.Id) && electiveGroup.Subjects.Any(member => member.SubjectId == subject.Id)
			select new TimetableNotificationChoice(electiveGroup.Id, subject.Id), token);

	internal async Task<IReadOnlyList<NotificationRecipientRefDto>> FindTeachersAsync(Guid tenantId, Guid[] references, CancellationToken token)
	{
		if (references.Length == 0) return [];
		var staff = await queries.ToListAsync((await employees.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
			x.UserId.HasValue && x.UserId != Guid.Empty &&
			(!x.EmployeeStatusType.HasValue || x.EmployeeStatusType == EmployeeStatusType.Active) &&
			(Enumerable.Contains(references, x.Id) || Enumerable.Contains(references, x.UserId.Value)))
			.Select(x => new { x.Id, UserId = x.UserId!.Value }), token);
		var resolved = new HashSet<NotificationRecipientRefDto>();
		foreach (var reference in references)
		{
			var matches = staff.Where(x => x.Id == reference || x.UserId == reference).Take(2).ToArray();
			if (matches.Length == 1)
				resolved.Add(new("AssignedTeacher", UserId: matches[0].UserId, EmployeeId: matches[0].Id));
		}
		return resolved.ToArray();
	}
}

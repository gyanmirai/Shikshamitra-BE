using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

namespace ShikshaMitra.Academic.Notifications;

internal sealed record ElectiveNoticeDivision(NotificationSourceScopeDto Scope, string Name);

[RemoteService(false)]
public sealed class ElectiveNotificationAudience(IRepository<Division, Guid> divisions, IRepository<Standard, Guid> standards,
	IRepository<AssignedSubject, Guid> assignments, IRepository<Subject, Guid> subjects, IRepository<ElectiveGroup, Guid> groups,
	IRepository<TeacherSubjectAllocation, Guid> divisionAssignments, IRepository<Student, Guid> students,
	AcademicNotificationEnrollmentRoster roster, TimetableNotificationAudience staff, IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal async Task<ElectiveNoticeDivision?> FindDivisionAsync(Guid school, Guid divisionId, CancellationToken token)
	{
		var placement = from division in await divisions.GetQueryableAsync()
			join standard in await standards.GetQueryableAsync() on division.StandardId equals standard.Id
			let stage = standard.EducationMediumStage
			let medium = stage.EducationMedium
			let board = medium.EducationBoard
			where division.Id == divisionId && division.TenantId == school && !division.IsDeleted &&
				standard.TenantId == school && !standard.IsDeleted && stage.TenantId == school && !stage.IsDeleted &&
				medium.TenantId == school && !medium.IsDeleted && board.TenantId == school && !board.IsDeleted &&
				board.AcademicYear.TenantId == school && !board.AcademicYear.IsDeleted
			select new ElectiveNoticeDivision(new(board.AcademicYearId, board.Id, medium.Id, standard.Id, division.Id),
				standard.Name + " - " + division.Name);
		return await queries.SingleOrDefaultAsync(placement, token);
	}

	internal async Task<ElectiveGroup?> FindGroupAsync(Guid school, Guid groupId, Guid? mediumId, CancellationToken token) =>
		await queries.SingleOrDefaultAsync((await groups.WithDetailsAsync(x => x.Subjects)).Where(x =>
			x.TenantId == school && x.Id == groupId && !x.IsDeleted && x.AcademicMediumId == mediumId &&
			x.Status == SubjectMasterStatus.Active), token);

	internal async Task<bool> IsEnrolledAsync(Guid school, Guid studentId, NotificationSourceScopeDto scope, CancellationToken token)
	{
		var eligible = (await students.GetQueryableAsync()).Where(x => x.TenantId == school && !x.IsDeleted &&
			x.Id == studentId && x.Status == StudentLifeCycleStatusEnum.Enrolled);
		var learners = await roster.FindAsync(school, scope.AcademicYearId!.Value, scope.MediumId!.Value, scope.ClassId!.Value,
			[scope.DivisionId!.Value], eligible, token);
		return learners?.Count == 1;
	}

	internal async Task<(AssignedSubject Assignment, string Name)?> FindPublishedAsync(Guid school, Guid groupId, Guid subjectId,
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

	internal async Task<IReadOnlyList<NotificationRecipientRefDto>> FindTeachersAsync(Guid school, AssignedSubject assignment,
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

	internal static Dictionary<string, NotificationVariableValueDto> Variables(string school, string division, string subject, string group) =>
		new(StringComparer.Ordinal)
		{
			["school_name"] = new(NotificationVariableType.Text, school),
			["class_name"] = new(NotificationVariableType.Text, division),
			["subject_name"] = new(NotificationVariableType.Text, subject),
			["group_name"] = new(NotificationVariableType.Text, group)
		};
}

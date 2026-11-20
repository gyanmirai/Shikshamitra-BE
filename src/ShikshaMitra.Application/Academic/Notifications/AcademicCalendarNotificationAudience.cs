using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class AcademicCalendarNotificationAudience(IRepository<Standard, Guid> standards,
	IRepository<Division, Guid> divisions, IRepository<AssignedSubject, Guid> assignedSubjects,
	IRepository<TeacherSubjectAllocation, Guid> sectionAllocations, IRepository<Student, Guid> students,
	AcademicNotificationEnrollmentRoster enrollment, TimetableNotificationAudience staff, IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal async Task<IReadOnlyList<NotificationSourceAudienceSliceDto>> ReadAsync(Guid school, NotificationSourceScopeDto medium,
		NotificationSourceScopeDto? requested, CancellationToken token)
	{
		if (medium.AcademicYearId is not Guid year || medium.MediumId is not Guid mediumId ||
			(requested != null && (requested with { ClassId = null, DivisionId = null }) != medium)) return [];
		var placements = from division in await divisions.GetQueryableAsync()
			join standard in await standards.GetQueryableAsync() on division.StandardId equals standard.Id
			let stage = standard.EducationMediumStage
			where division.TenantId == school && !division.IsDeleted && standard.TenantId == school && !standard.IsDeleted &&
				stage.TenantId == school && !stage.IsDeleted && stage.EducationMediumId == mediumId &&
				(requested == null || (!requested.ClassId.HasValue || requested.ClassId == standard.Id) &&
					(!requested.DivisionId.HasValue || requested.DivisionId == division.Id))
			select new { ClassId = standard.Id, DivisionId = division.Id };
		var rows = await queries.ToListAsync(placements, token);
		if (rows.Count == 0) return [];

		var classIds = rows.Select(x => x.ClassId).Distinct().ToArray();
		var divisionIds = rows.Select(x => x.DivisionId).ToArray();
		var publishedAssignments = (await assignedSubjects.GetQueryableAsync()).Where(x =>
			x.TenantId == school && !x.IsDeleted && x.Status == SubjectStatus.Published && Enumerable.Contains(classIds, x.StandardId));
		var classTeachers = await queries.ToListAsync(publishedAssignments
			.SelectMany(assignment => assignment.Teachers,
				(assignment, teacher) => new { ClassId = assignment.StandardId, teacher.TeacherId, teacher.TenantId })
			.Where(teacher => teacher.TenantId == school).Distinct(), token);
		var sectionTeachers = await queries.ToListAsync((await sectionAllocations.GetQueryableAsync()).Where(x =>
			!x.IsDeleted && Enumerable.Contains(divisionIds, x.DivisionId) &&
			placements.Any(placement => placement.ClassId == x.StandardId && placement.DivisionId == x.DivisionId) &&
			publishedAssignments.Any(assignment => assignment.StandardId == x.StandardId && assignment.SubjectId == x.SubjectId))
			.SelectMany(allocation => allocation.Teachers,
				(allocation, teacher) => new { allocation.DivisionId, teacher.TeacherId, teacher.IsDeleted })
			.Where(teacher => !teacher.IsDeleted).Distinct(), token);

		var staffRefs = classTeachers.Select(x => x.TeacherId).Concat(sectionTeachers.Select(x => x.TeacherId))
			.Where(id => id != Guid.Empty).Distinct().ToArray();
		var teachers = await staff.FindTeachersAsync(school, staffRefs, token);
		var onlyCurrent = (await students.GetQueryableAsync()).Where(x =>
			x.TenantId == school && !x.IsDeleted && x.Status == StudentLifeCycleStatusEnum.Enrolled);
		var byClass = classTeachers.ToLookup(x => x.ClassId, x => x.TeacherId);
		var byDivision = sectionTeachers.ToLookup(x => x.DivisionId, x => x.TeacherId);
		var slices = new List<NotificationSourceAudienceSliceDto>();
		foreach (var group in rows.GroupBy(x => x.ClassId))
		{
			var sections = group.Select(x => x.DivisionId).ToArray();
			var learners = await enrollment.FindAsync(school, year, mediumId, group.Key, sections, onlyCurrent, token);
			if (learners == null) return [];
			var learnersByDivision = learners.ToLookup(x => x.DivisionId, x => x.StudentId);
			foreach (var divisionId in sections)
			{
				var assigned = byClass[group.Key].Concat(byDivision[divisionId]).ToHashSet();
				var recipients = teachers.Where(x => assigned.Contains(x.EmployeeId!.Value) || assigned.Contains(x.UserId!.Value))
					.Concat(learnersByDivision[divisionId].Select(id => new NotificationRecipientRefDto("Student", StudentId: id))).ToArray();
				if (recipients.Length != 0)
					slices.Add(new(medium with { ClassId = group.Key, DivisionId = divisionId }, recipients));
			}
		}
		return slices;
	}
}

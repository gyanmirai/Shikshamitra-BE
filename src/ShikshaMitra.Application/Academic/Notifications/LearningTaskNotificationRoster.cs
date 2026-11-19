using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class LearningTaskNotificationRoster(IRepository<Student, Guid> students, AcademicNotificationEnrollmentRoster enrollment,
	IRepository<StudentElectiveSelection, Guid> electiveSelections, IRepository<StudentSubmissionEntity, Guid> submissions) : ITransientDependency
{
	internal async Task<IReadOnlyList<AcademicNotificationLearner>?> FindAsync(Guid tenantId, LearningTaskNotificationRecord record,
		Guid[] divisions, Guid[]? capturedStudents, bool pendingOnly, CancellationToken cancellationToken)
	{
		var taskId = record.Task.Id;
		var isAssignment = record.Task.IsAssignment;
		var yearId = record.Placement.YearId;
		var classId = record.Task.StandardId;
		var mediumId = record.Placement.MediumId;
		var subjectId = record.Subject.Id;
		var electiveGroup = record.Subject.ElectiveGroupId;
		var selected = (await electiveSelections.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
			x.AcademicMediumId == mediumId && x.ElectiveGroupId == electiveGroup && x.SelectedSubjectId == subjectId && x.IsLocked);
		var live = (await students.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
			x.Status == StudentLifeCycleStatusEnum.Enrolled &&
			(!electiveGroup.HasValue || selected.Any(s => s.StudentId == x.Id)));
		if (pendingOnly)
		{
			var submitted = (await submissions.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
				(isAssignment ? x.AssignmentId == taskId : x.HomeworkAssignmentId == taskId && x.AssignmentId == null) && (x.SubmissionDate.HasValue ||
					x.Status == SubmissionStatus.Submitted || x.Status == SubmissionStatus.Late ||
					x.Status == SubmissionStatus.Excused || x.Status == SubmissionStatus.Reviewed || x.Status == SubmissionStatus.NeedsReview));
			live = live.Where(x => !submitted.Any(s => s.StudentId == x.Id));
		}
		if (capturedStudents != null) live = live.Where(x => Enumerable.Contains(capturedStudents, x.Id));

		return await enrollment.FindAsync(tenantId, yearId, mediumId, classId, divisions, live, cancellationToken);
	}
}

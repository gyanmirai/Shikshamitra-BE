using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record HomeworkReminderLearner(Guid StudentId, Guid DivisionId);

[RemoteService(false)]
public sealed class HomeworkReminderRoster(IRepository<Student, Guid> students, IRepository<StudentEnrollment, Guid> enrollments,
	IRepository<StudentElectiveSelection, Guid> electiveSelections, IRepository<StudentSubmissionEntity, Guid> submissions,
	IDataFilter filters, IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal async Task<IReadOnlyList<HomeworkReminderLearner>?> FindPendingAsync(Guid tenantId, HomeworkReminderRecord record,
		Guid[] divisions, Guid[]? capturedStudents, CancellationToken cancellationToken)
	{
		var taskId = record.Task.Id;
		var yearId = record.Placement.YearId;
		var classId = record.Task.StandardId;
		var mediumId = record.Placement.MediumId;
		var subjectId = record.Subject.Id;
		var electiveGroup = record.Subject.ElectiveGroupId;
		var submitted = (await submissions.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
			x.HomeworkAssignmentId == taskId && x.AssignmentId == null && (x.SubmissionDate.HasValue ||
				x.Status == SubmissionStatus.Submitted || x.Status == SubmissionStatus.Late ||
				x.Status == SubmissionStatus.Excused || x.Status == SubmissionStatus.Reviewed || x.Status == SubmissionStatus.NeedsReview));
		var selected = (await electiveSelections.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
			x.AcademicMediumId == mediumId && x.ElectiveGroupId == electiveGroup && x.SelectedSubjectId == subjectId && x.IsLocked);
		var live = (await students.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
			x.Status == StudentLifeCycleStatusEnum.Enrolled && !submitted.Any(s => s.StudentId == x.Id) &&
			(!electiveGroup.HasValue || selected.Any(s => s.StudentId == x.Id)));
		if (capturedStudents != null) live = live.Where(x => Enumerable.Contains(capturedStudents, x.Id));

		// Include revoked enrollments in the legacy check so the old Student placement cannot restore access.
		using var softDelete = filters.Disable<ISoftDelete>();
		var history = (await enrollments.GetQueryableAsync()).Where(x => x.TenantId == tenantId && x.AcademicYearId == yearId);
		var active = history.Where(x => !x.IsDeleted && x.IsActive);
		var enrolled = from enrollment in active
			join student in live on enrollment.StudentId equals student.Id
			where enrollment.StandardId == classId && enrollment.DivisionId.HasValue &&
				Enumerable.Contains(divisions, enrollment.DivisionId.Value) &&
				!active.Any(other => other.StudentId == student.Id && other.Id != enrollment.Id)
			select new { StudentId = student.Id, DivisionId = enrollment.DivisionId!.Value };
		var legacy = live.Where(x => x.AcademicYearId == yearId && x.AcademicMediumId == mediumId &&
			x.StandardId == classId && x.DivisionId.HasValue && Enumerable.Contains(divisions, x.DivisionId.Value) &&
			!history.Any(enrollment => enrollment.StudentId == x.Id))
			.Select(x => new { StudentId = x.Id, DivisionId = x.DivisionId!.Value });
		var rows = await queries.ToListAsync(enrolled.Concat(legacy).Take(10001)
			.Select(x => new HomeworkReminderLearner(x.StudentId, x.DivisionId)), cancellationToken);
		return rows.Count <= 10000 ? rows : null;
	}
}

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.BackgroundWork;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class AssignmentScheduledPublicationService(
	IRepository<AssignmentEntity, Guid> assignmentRepository,
	AcademicBackgroundBatchRunner batchRunner) : DomainService, ITransientDependency
{
	public Task<int> PublishDueSchedulesAsync(DateTime now, CancellationToken cancellationToken = default)
	{
		return batchRunner.RunAsync(assignmentRepository,
				assignment => assignment.Status == AssignmentStatus.Published &&
					assignment.DivisionSchedules.Any(schedule =>
						!schedule.IsPublished &&
						(!schedule.PublishDate.HasValue || schedule.PublishDate.Value <= now)),
			assignment =>
			{
				if (assignment.Status != AssignmentStatus.Published) return 0;
				var before = assignment.DivisionSchedules.Count(schedule => schedule.IsPublished);
				assignment.PublishDueSchedules(now);
				return assignment.DivisionSchedules.Count(schedule => schedule.IsPublished) - before;
			}, cancellationToken, assignment => assignment.DivisionSchedules);
	}
}

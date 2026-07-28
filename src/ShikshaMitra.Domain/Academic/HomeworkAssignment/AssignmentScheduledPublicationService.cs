using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class AssignmentScheduledPublicationService(
	IRepository<AssignmentEntity, Guid> assignmentRepository,
	IDataFilter dataFilter) : DomainService, ITransientDependency
{
	public async Task<int> PublishDueSchedulesAsync(DateTime now)
	{
		var publishedCount = 0;
		using (dataFilter.Disable<IMultiTenant>())
		{
			var assignments = await assignmentRepository.GetListAsync(
				assignment => assignment.Status == AssignmentStatus.Published &&
					assignment.DivisionSchedules.Any(schedule =>
						!schedule.IsPublished &&
						(!schedule.PublishDate.HasValue || schedule.PublishDate.Value <= now)),
				includeDetails: true);

			foreach (var assignment in assignments)
			{
				var before = assignment.DivisionSchedules.Count(schedule => schedule.IsPublished);
				assignment.PublishDueSchedules(now);
				var changed = assignment.DivisionSchedules.Count(schedule => schedule.IsPublished) - before;
				if (changed == 0)
				{
					continue;
				}

				await assignmentRepository.UpdateAsync(assignment, autoSave: false);
				publishedCount += changed;
			}
		}

		return publishedCount;
	}
}

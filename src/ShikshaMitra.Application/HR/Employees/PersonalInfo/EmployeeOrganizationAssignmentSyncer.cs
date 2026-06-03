using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

public class EmployeeOrganizationAssignmentSyncer(
	IRepository<EmployeeOrganizationAssignment, Guid> employeeOrganizationAssignmentRepository,
	ICurrentTenant currentTenant,
	IGuidGenerator guidGenerator,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public async Task SyncAsync(Guid employeeId, IReadOnlyCollection<Guid>? organizationIds)
	{
		var requestedStageIds = (organizationIds ?? Array.Empty<Guid>())
			.Where(x => x != Guid.Empty)
			.Distinct()
			.ToHashSet();

		var query = await employeeOrganizationAssignmentRepository.GetQueryableAsync();
		var existing = await asyncExecuter.ToListAsync(query.Where(x => x.EmployeeId == employeeId));
		var existingStageIds = existing.Select(x => x.EducationMediumStageId).ToHashSet();

		foreach (var assignment in existing.Where(x => !requestedStageIds.Contains(x.EducationMediumStageId)))
		{
			await employeeOrganizationAssignmentRepository.DeleteAsync(assignment, autoSave: false);
		}

		foreach (var stageId in requestedStageIds.Except(existingStageIds))
		{
			await employeeOrganizationAssignmentRepository.InsertAsync(
				new EmployeeOrganizationAssignment(
					guidGenerator.Create(),
					currentTenant.Id,
					employeeId,
					stageId),
				autoSave: false);
		}
	}
}

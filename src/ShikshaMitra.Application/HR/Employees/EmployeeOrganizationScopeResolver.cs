using ShikshaMitra.Security.UserScope;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using static ShikshaMitra.HR.Employees.EmployeeOrganizationScopeHierarchy;

namespace ShikshaMitra.HR.Employees;

public class EmployeeOrganizationScopeResolver(
	IRepository<UserScopeAssignment, Guid> userScopeAssignmentRepository,
	EmployeeOrganizationScopeLookupReader lookupReader,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task<Dictionary<Guid, List<EmployeeOrganizationDto>>> GetOrganizationsByUserIdsAsync(
		IEnumerable<Guid?> userIds,
		Guid? academicYearId = null)
	{
		var distinctUserIds = userIds
			.Where(x => x.HasValue && x.Value != Guid.Empty)
			.Select(x => x!.Value)
			.Distinct()
			.ToList();

		var result = distinctUserIds.ToDictionary(x => x, _ => new List<EmployeeOrganizationDto>());
		if (distinctUserIds.Count == 0)
		{
			return result;
		}

		var assignmentQuery = (await userScopeAssignmentRepository.GetQueryableAsync())
			.Where(x => x.UserId.HasValue &&
						distinctUserIds.Contains(x.UserId.Value) &&
						x.IsActive);

		var assignments = await asyncExecuter.ToListAsync(assignmentQuery);
		if (assignments.Count == 0)
		{
			return result;
		}

		var stageInfos = await lookupReader.LoadStageInfosAsync(academicYearId);
		var stageInfoById = stageInfos.ToDictionary(x => x.Id);
		var standardStageLookup = await lookupReader.LoadStandardStageLookupAsync(assignments);
		var divisionStageLookup = await lookupReader.LoadDivisionStageLookupAsync(assignments);
		var stageIdsByUserId = distinctUserIds.ToDictionary(x => x, _ => new HashSet<Guid>());

		foreach (var assignment in assignments)
		{
			if (!assignment.UserId.HasValue || !stageIdsByUserId.TryGetValue(assignment.UserId.Value, out var stageIds))
			{
				continue;
			}

			AddResolvedStageIds(
				stageIds,
				assignment,
				stageInfos,
				stageInfoById,
				standardStageLookup,
				divisionStageLookup);
		}

		foreach (var userId in distinctUserIds)
		{
			result[userId] = stageIdsByUserId[userId]
				.Where(stageInfoById.ContainsKey)
				.Select(id => stageInfoById[id])
				.OrderBy(x => x.Name)
				.Select(x => new EmployeeOrganizationDto
				{
					Id = x.Id,
					Name = x.Name
				})
				.ToList();
		}

		return result;
	}

	public async Task<HashSet<Guid>> GetUserIdsWithAnyStageAsync(
		IEnumerable<Guid> mediumStageIds,
		Guid? academicYearId = null)
	{
		var targetStageIds = mediumStageIds
			.Where(x => x != Guid.Empty)
			.Distinct()
			.ToHashSet();
		if (targetStageIds.Count == 0)
		{
			return new HashSet<Guid>();
		}

		var assignmentQuery = (await userScopeAssignmentRepository.GetQueryableAsync())
			.Where(x => x.UserId.HasValue && x.IsActive);
		var assignments = await asyncExecuter.ToListAsync(assignmentQuery);
		if (assignments.Count == 0)
		{
			return new HashSet<Guid>();
		}

		var stageInfos = await lookupReader.LoadStageInfosAsync(academicYearId);
		var stageInfoById = stageInfos.ToDictionary(x => x.Id);
		var standardStageLookup = await lookupReader.LoadStandardStageLookupAsync(assignments);
		var divisionStageLookup = await lookupReader.LoadDivisionStageLookupAsync(assignments);
		var matchingUserIds = new HashSet<Guid>();

		foreach (var assignment in assignments)
		{
			var resolvedStageIds = new HashSet<Guid>();
			AddResolvedStageIds(
				resolvedStageIds,
				assignment,
				stageInfos,
				stageInfoById,
				standardStageLookup,
				divisionStageLookup);

			if (assignment.UserId.HasValue && resolvedStageIds.Overlaps(targetStageIds))
			{
				matchingUserIds.Add(assignment.UserId.Value);
			}
		}

		return matchingUserIds;
	}

	public async Task<bool> UserHasStageAsync(Guid? userId, Guid mediumStageId, Guid? academicYearId = null)
	{
		if (!userId.HasValue || userId.Value == Guid.Empty || mediumStageId == Guid.Empty)
		{
			return false;
		}

		var organizations = await GetOrganizationsByUserIdsAsync([userId.Value], academicYearId);
		return organizations.TryGetValue(userId.Value, out var stages) && stages.Any(x => x.Id == mediumStageId);
	}

}

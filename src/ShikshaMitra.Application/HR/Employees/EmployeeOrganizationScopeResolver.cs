using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Employees;

public class EmployeeOrganizationScopeResolver(
	IRepository<UserScopeAssignment, Guid> userScopeAssignmentRepository,
	IRepository<EducationMediumStage, Guid> educationMediumStageRepository,
	IRepository<EducationMedium, Guid> educationMediumRepository,
	IRepository<EducationBoard, Guid> educationBoardRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
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

		var stageInfos = await LoadStageInfosAsync(academicYearId);
		var stageInfoById = stageInfos.ToDictionary(x => x.Id);
		var standardStageLookup = await LoadStandardStageLookupAsync(assignments);
		var divisionStageLookup = await LoadDivisionStageLookupAsync(assignments);
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

		var stageInfos = await LoadStageInfosAsync(academicYearId);
		var stageInfoById = stageInfos.ToDictionary(x => x.Id);
		var standardStageLookup = await LoadStandardStageLookupAsync(assignments);
		var divisionStageLookup = await LoadDivisionStageLookupAsync(assignments);
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

	private async Task<List<StageInfo>> LoadStageInfosAsync(Guid? academicYearId)
	{
		var stageQuery = await educationMediumStageRepository.GetQueryableAsync();
		var mediumQuery = await educationMediumRepository.GetQueryableAsync();
		var boardQuery = await educationBoardRepository.GetQueryableAsync();

		var query =
			from stage in stageQuery
			join medium in mediumQuery on stage.EducationMediumId equals medium.Id
			join board in boardQuery on medium.EducationBoardId equals board.Id
			where !academicYearId.HasValue || board.AcademicYearId == academicYearId.Value
			select new StageInfo
			{
				Id = stage.Id,
				Name = stage.Name,
				MediumId = medium.Id,
				BoardId = board.Id,
				AcademicYearId = board.AcademicYearId
			};

		return await asyncExecuter.ToListAsync(query);
	}

	private async Task<Dictionary<Guid, Guid>> LoadStandardStageLookupAsync(
		IReadOnlyCollection<UserScopeAssignment> assignments)
	{
		var standardIds = assignments
			.Where(x => x.StandardId.HasValue)
			.Select(x => x.StandardId!.Value)
			.Distinct()
			.ToList();
		if (standardIds.Count == 0)
		{
			return new Dictionary<Guid, Guid>();
		}

		var standardQuery = await standardRepository.GetQueryableAsync();
		var rows = await asyncExecuter.ToListAsync(
			standardQuery
				.Where(x => standardIds.Contains(x.Id))
				.Select(x => new { x.Id, x.EducationMediumStageId }));

		return rows.ToDictionary(x => x.Id, x => x.EducationMediumStageId);
	}

	private async Task<Dictionary<Guid, Guid>> LoadDivisionStageLookupAsync(
		IReadOnlyCollection<UserScopeAssignment> assignments)
	{
		var divisionIds = assignments
			.Where(x => x.DivisionId.HasValue)
			.Select(x => x.DivisionId!.Value)
			.Distinct()
			.ToList();
		if (divisionIds.Count == 0)
		{
			return new Dictionary<Guid, Guid>();
		}

		var divisionQuery = await divisionRepository.GetQueryableAsync();
		var standardQuery = await standardRepository.GetQueryableAsync();
		var rows = await asyncExecuter.ToListAsync(
			from division in divisionQuery
			join standard in standardQuery on division.StandardId equals standard.Id
			where divisionIds.Contains(division.Id)
			select new
			{
				division.Id,
				standard.EducationMediumStageId
			});

		return rows.ToDictionary(x => x.Id, x => x.EducationMediumStageId);
	}

	private static void AddResolvedStageIds(
		HashSet<Guid> stageIds,
		UserScopeAssignment assignment,
		IReadOnlyCollection<StageInfo> stageInfos,
		IReadOnlyDictionary<Guid, StageInfo> stageInfoById,
		IReadOnlyDictionary<Guid, Guid> standardStageLookup,
		IReadOnlyDictionary<Guid, Guid> divisionStageLookup)
	{
		if (assignment.EducationMediumStageId.HasValue)
		{
			AddIfInScope(stageIds, assignment.EducationMediumStageId.Value, stageInfoById);
		}

		if (assignment.StandardId.HasValue &&
			standardStageLookup.TryGetValue(assignment.StandardId.Value, out var standardStageId))
		{
			AddIfInScope(stageIds, standardStageId, stageInfoById);
		}

		if (assignment.DivisionId.HasValue &&
			divisionStageLookup.TryGetValue(assignment.DivisionId.Value, out var divisionStageId))
		{
			AddIfInScope(stageIds, divisionStageId, stageInfoById);
		}

		if (assignment.StandardId.HasValue || assignment.DivisionId.HasValue || assignment.EducationMediumStageId.HasValue)
		{
			return;
		}

		IEnumerable<StageInfo> expandedStages = stageInfos;
		if (assignment.EducationMediumId.HasValue)
		{
			expandedStages = expandedStages.Where(x => x.MediumId == assignment.EducationMediumId.Value);
		}
		else if (assignment.EducationBoardId.HasValue)
		{
			expandedStages = expandedStages.Where(x => x.BoardId == assignment.EducationBoardId.Value);
		}
		else if (assignment.AcademicYearId.HasValue)
		{
			expandedStages = expandedStages.Where(x => x.AcademicYearId == assignment.AcademicYearId.Value);
		}

		foreach (var stage in expandedStages)
		{
			stageIds.Add(stage.Id);
		}
	}

	private static void AddIfInScope(
		HashSet<Guid> stageIds,
		Guid stageId,
		IReadOnlyDictionary<Guid, StageInfo> stageInfoById)
	{
		if (stageInfoById.ContainsKey(stageId))
		{
			stageIds.Add(stageId);
		}
	}

	private sealed class StageInfo
	{
		public Guid Id { get; init; }
		public string Name { get; init; } = string.Empty;
		public Guid MediumId { get; init; }
		public Guid BoardId { get; init; }
		public Guid AcademicYearId { get; init; }
	}
}

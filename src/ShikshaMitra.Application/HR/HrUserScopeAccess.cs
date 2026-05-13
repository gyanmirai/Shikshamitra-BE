using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR;

public static class HrUserScopeAccess
{
	public static bool HasHrWideAccess(UserScopeContext scope) =>
		scope.HasTenantWideScope();

	public static bool HasAnyConcreteHrStageAccess(UserScopeContext scope) =>
		HasHrWideAccess(scope) ||
		scope.MediumStageIds.Count > 0 ||
		scope.WildcardBoardAcademicYearIds.Count > 0 ||
		scope.WildcardMediumBoardIds.Count > 0 ||
		scope.WildcardMediumStageMediumIds.Count > 0 ||
		scope.BoardIds.Count > 0 ||
		scope.MediumIds.Count > 0;

	public static bool CanAccessEmployeeStage(UserScopeContext scope, Guid? employeeStageId) =>
		HasHrWideAccess(scope) ||
		(employeeStageId.HasValue && scope.CanAccessMediumStage(employeeStageId.Value));

	public static List<Guid> FilterEmployeeIdsByStage(
		IReadOnlyCollection<Guid> employeeIds,
		IReadOnlyDictionary<Guid, Guid?> employeeStageMap,
		UserScopeContext scope,
		bool includeUnknownStageEmployees)
	{
		if (HasHrWideAccess(scope))
		{
			return employeeIds.ToList();
		}

		return employeeIds
			.Where(id =>
			{
				if (!employeeStageMap.TryGetValue(id, out var stageId) || !stageId.HasValue)
				{
					return includeUnknownStageEmployees;
				}

				return scope.CanAccessMediumStage(stageId.Value);
			})
			.ToList();
	}
}

[RemoteService(false)]
public class HrUserScopeAccessService : ApplicationService, ITransientDependency
{
	private readonly IRepository<EducationMediumStage, Guid> _stageRepository;
	private readonly IRepository<EducationMedium, Guid> _mediumRepository;
	private readonly IRepository<EducationBoard, Guid> _boardRepository;

	public HrUserScopeAccessService(
		IRepository<EducationMediumStage, Guid> stageRepository,
		IRepository<EducationMedium, Guid> mediumRepository,
		IRepository<EducationBoard, Guid> boardRepository)
	{
		_stageRepository = stageRepository;
		_mediumRepository = mediumRepository;
		_boardRepository = boardRepository;
	}

	public async Task<bool> CanAccessEmployeeStageAsync(UserScopeContext scope, Guid? employeeStageId)
	{
		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return true;
		}

		if (!employeeStageId.HasValue)
		{
			return false;
		}

		var stageScope = await ResolveStageScopeAsync(employeeStageId.Value);
		return scope.CanAccessMediumStage(
			employeeStageId.Value,
			stageScope?.MediumId,
			stageScope?.BoardId,
			stageScope?.AcademicYearId);
	}

	public async Task<List<Guid>> FilterEmployeeIdsByStageAsync(
		IReadOnlyCollection<Guid> employeeIds,
		IReadOnlyDictionary<Guid, Guid?> employeeStageMap,
		UserScopeContext scope,
		bool includeUnknownStageEmployees)
	{
		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return employeeIds.ToList();
		}

		var stageIds = employeeStageMap.Values
			.Where(x => x.HasValue)
			.Select(x => x!.Value)
			.Distinct()
			.ToList();
		var stageScopes = await ResolveStageScopesAsync(stageIds);

		return employeeIds
			.Where(id =>
			{
				if (!employeeStageMap.TryGetValue(id, out var stageId) || !stageId.HasValue)
				{
					return includeUnknownStageEmployees;
				}

				stageScopes.TryGetValue(stageId.Value, out var parentScope);
				return scope.CanAccessMediumStage(
					stageId.Value,
					parentScope?.MediumId,
					parentScope?.BoardId,
					parentScope?.AcademicYearId);
			})
			.ToList();
	}

	public async Task<List<Guid>> FilterEmployeeIdsByStagesAsync(
		IReadOnlyCollection<Guid> employeeIds,
		IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> employeeStageMap,
		UserScopeContext scope,
		bool includeUnknownStageEmployees)
	{
		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return employeeIds.ToList();
		}

		var stageIds = employeeStageMap.Values
			.SelectMany(x => x)
			.Distinct()
			.ToList();
		var stageScopes = await ResolveStageScopesAsync(stageIds);

		return employeeIds
			.Where(id =>
			{
				if (!employeeStageMap.TryGetValue(id, out var employeeStageIds) || employeeStageIds.Count == 0)
				{
					return includeUnknownStageEmployees;
				}

				return employeeStageIds.Any(stageId =>
				{
					stageScopes.TryGetValue(stageId, out var parentScope);
					return scope.CanAccessMediumStage(
						stageId,
						parentScope?.MediumId,
						parentScope?.BoardId,
						parentScope?.AcademicYearId);
				});
			})
			.ToList();
	}

	private async Task<Dictionary<Guid, MediumStageScope>> ResolveStageScopesAsync(IReadOnlyCollection<Guid> stageIds)
	{
		if (stageIds.Count == 0)
		{
			return new Dictionary<Guid, MediumStageScope>();
		}

		var stages = await AsyncExecuter.ToListAsync(
			(await _stageRepository.GetQueryableAsync()).Where(x => stageIds.Contains(x.Id)));
		var mediumIds = stages.Select(x => x.EducationMediumId).Distinct().ToList();
		var mediums = await AsyncExecuter.ToListAsync(
			(await _mediumRepository.GetQueryableAsync()).Where(x => mediumIds.Contains(x.Id)));
		var mediumById = mediums.ToDictionary(x => x.Id);
		var boardIds = mediums.Select(x => x.EducationBoardId).Distinct().ToList();
		var boards = await AsyncExecuter.ToListAsync(
			(await _boardRepository.GetQueryableAsync()).Where(x => boardIds.Contains(x.Id)));
		var boardById = boards.ToDictionary(x => x.Id);

		var result = new Dictionary<Guid, MediumStageScope>();
		foreach (var stage in stages)
		{
			if (!mediumById.TryGetValue(stage.EducationMediumId, out var medium) ||
			    !boardById.TryGetValue(medium.EducationBoardId, out var board))
			{
				continue;
			}

			result[stage.Id] = new MediumStageScope(stage.Id, medium.Id, board.Id, board.AcademicYearId);
		}

		return result;
	}

	private async Task<MediumStageScope?> ResolveStageScopeAsync(Guid stageId)
	{
		var scopes = await ResolveStageScopesAsync(new[] { stageId });
		return scopes.TryGetValue(stageId, out var scope) ? scope : null;
	}

	private sealed record MediumStageScope(Guid StageId, Guid MediumId, Guid BoardId, Guid AcademicYearId);
}

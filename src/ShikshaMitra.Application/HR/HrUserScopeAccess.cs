using ShikshaMitra.Security.UserScope;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

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
public class HrUserScopeAccessService(HrMediumStageScopeReader stageScopeReader)
	: ApplicationService, ITransientDependency
{
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

		var stageScopes = await stageScopeReader.GetByStageIdsAsync([employeeStageId.Value]);
		stageScopes.TryGetValue(employeeStageId.Value, out var stageScope);
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
		var stageScopes = await stageScopeReader.GetByStageIdsAsync(stageIds);

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
		var stageScopes = await stageScopeReader.GetByStageIdsAsync(stageIds);

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

	public async Task<List<Guid>> ResolveAccessibleStageIdsAsync(UserScopeContext scope)
	{
		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return new List<Guid>();
		}

		var rows = await stageScopeReader.GetAllAsync();

		return rows
			.Where(x => scope.CanAccessMediumStage(x.StageId, x.MediumId, x.BoardId, x.AcademicYearId))
			.Select(x => x.StageId)
			.Distinct()
			.ToList();
	}
}

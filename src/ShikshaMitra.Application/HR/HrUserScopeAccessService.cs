using ShikshaMitra.Security.UserScope;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR;

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

	public void LogDeniedAccess(
		UserScopeContext scope,
		string operation,
		string? targetType = null,
		Guid? targetId = null,
		Guid? stageId = null)
	{
		Logger.LogInformation(
			"HR UserScope denied direct access. Operation: {Operation}, UserId: {UserId}, TenantId: {TenantId}, TargetType: {TargetType}, TargetId: {TargetId}, StageId: {StageId}, ScopeKey: {ScopeKey}",
			operation,
			scope.UserId,
			scope.TenantId,
			targetType,
			targetId,
			stageId,
			HrUserScopeAccess.BuildScopeSummaryKey(scope));
	}

	public void EnsureHasConcreteHrAccess(UserScopeContext scope, string operation)
	{
		if (HrUserScopeAccess.HasAnyConcreteHrStageAccess(scope))
		{
			return;
		}

		LogDeniedAccess(scope, operation);
		throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
	}

	public async Task EnsureCanAccessEmployeeStageAsync(
		UserScopeContext scope,
		Guid? employeeStageId,
		string operation,
		Guid? employeeId = null)
	{
		if (await CanAccessEmployeeStageAsync(scope, employeeStageId))
		{
			return;
		}

		LogDeniedAccess(scope, operation, "Employee", employeeId, employeeStageId);
		throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
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

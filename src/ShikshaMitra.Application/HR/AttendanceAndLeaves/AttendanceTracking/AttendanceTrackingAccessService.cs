using ShikshaMitra.HR;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[RemoteService(false)]
public class AttendanceTrackingAccessService(
	IUserScopeService userScopeService,
	HrUserScopeAccessService hrScopeAccessService,
	HrEmployeeScopeQueryService employeeScopeQueryService,
	HrMediumStageScopeReader stageScopeReader)
	: ApplicationService, ITransientDependency
{
	internal async Task<UserScopeContext> EnsureCanReadAttendanceTrackingAsync()
	{
		var uid = CurrentUser.Id;
		if (!uid.HasValue)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);
		}

		var scope = await userScopeService.GetUserScopeAsync(uid.Value);
		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return scope;
		}

		hrScopeAccessService.EnsureHasConcreteHrAccess(scope, nameof(EnsureCanReadAttendanceTrackingAsync));

		return scope;
	}

	internal async Task EnsureMediumStageInScopeAsync(UserScopeContext scope, Guid educationMediumStageId)
	{
		await hrScopeAccessService.EnsureCanAccessEmployeeStageAsync(
			scope,
			educationMediumStageId,
			nameof(EnsureMediumStageInScopeAsync));
	}

	internal async Task<List<Guid>> ResolveEmployeeWiseStageIdsAsync(
		UserScopeContext scope,
		Guid employeeId,
		Guid? educationMediumStageId,
		Guid? academicYearId)
	{
		var employeeStageLookup = await employeeScopeQueryService.BuildEmployeeStageLookupAsync([employeeId]);
		if (!employeeStageLookup.TryGetValue(employeeId, out var employeeStageIds) || employeeStageIds.Count == 0)
		{
			hrScopeAccessService.LogDeniedAccess(scope, nameof(ResolveEmployeeWiseStageIdsAsync), "Employee", employeeId);
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
		}

		var distinctStageIds = employeeStageIds.Distinct().ToList();
		if (educationMediumStageId.HasValue)
		{
			await EnsureMediumStageInScopeAsync(scope, educationMediumStageId.Value);
			if (!distinctStageIds.Contains(educationMediumStageId.Value))
			{
				hrScopeAccessService.LogDeniedAccess(
					scope,
					nameof(ResolveEmployeeWiseStageIdsAsync),
					"Employee",
					employeeId,
					educationMediumStageId.Value);
				throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
			}

			if (academicYearId.HasValue && !await StageBelongsToAcademicYearAsync(educationMediumStageId.Value, academicYearId.Value))
			{
				throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
			}
			return [educationMediumStageId.Value];
		}

		if (academicYearId.HasValue)
		{
			distinctStageIds = await FilterStageIdsByAcademicYearAsync(distinctStageIds, academicYearId.Value);
			if (distinctStageIds.Count == 0)
			{
				hrScopeAccessService.LogDeniedAccess(scope, nameof(ResolveEmployeeWiseStageIdsAsync), "Employee", employeeId);
				throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
			}
		}

		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return distinctStageIds;
		}

		var accessibleStageIds = new List<Guid>();
		foreach (var stageId in distinctStageIds)
		{
			if (await hrScopeAccessService.CanAccessEmployeeStageAsync(scope, stageId))
			{
				accessibleStageIds.Add(stageId);
			}
		}

		if (accessibleStageIds.Count == 0)
		{
			hrScopeAccessService.LogDeniedAccess(scope, nameof(ResolveEmployeeWiseStageIdsAsync), "Employee", employeeId);
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
		}

		return accessibleStageIds;
	}

	private async Task<bool> StageBelongsToAcademicYearAsync(Guid stageId, Guid academicYearId)
	{
		var stageScopes = await stageScopeReader.GetByStageIdsAsync([stageId]);
		return stageScopes.TryGetValue(stageId, out var stageScope) &&
		       stageScope.AcademicYearId == academicYearId;
	}

	private async Task<List<Guid>> FilterStageIdsByAcademicYearAsync(
		IReadOnlyCollection<Guid> stageIds,
		Guid academicYearId)
	{
		var stageScopes = await stageScopeReader.GetByStageIdsAsync(stageIds);
		return stageIds
			.Where(stageId =>
				stageScopes.TryGetValue(stageId, out var stageScope) &&
				stageScope.AcademicYearId == academicYearId)
			.Distinct()
			.ToList();
	}
}

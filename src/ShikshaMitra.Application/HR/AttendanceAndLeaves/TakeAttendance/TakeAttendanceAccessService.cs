using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;

[RemoteService(false)]
public class TakeAttendanceAccessService(
	IUserScopeService userScopeService,
	HrUserScopeAccessService hrScopeAccessService)
	: ApplicationService, ITransientDependency
{
	public async Task<UserScopeContext> EnsureCanOperateAttendanceAsync()
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

		if (!HrUserScopeAccess.HasAnyConcreteHrStageAccess(scope))
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
		}

		return scope;
	}

	public async Task EnsureEmployeeScopeAllowedAsync(UserScopeContext scope, Guid? employeeStageId)
	{
		if (await hrScopeAccessService.CanAccessEmployeeStageAsync(scope, employeeStageId))
		{
			return;
		}

		throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
	}

	public async Task<HashSet<Guid>> ResolveAccessibleStageIdsAsync(UserScopeContext scope) =>
		HrUserScopeAccess.HasHrWideAccess(scope)
			? []
			: (await hrScopeAccessService.ResolveAccessibleStageIdsAsync(scope)).ToHashSet();

	public static Guid? ResolveScopedStageFallback(UserScopeContext scope, Guid? employeeStageId)
	{
		if (employeeStageId.HasValue || HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return employeeStageId;
		}

		return scope.MediumStageIds.Count == 1 ? scope.MediumStageIds[0] : null;
	}

	public static Guid? ResolveAssignedStageForScope(
		IReadOnlyCollection<Guid> assignedStages,
		UserScopeContext scope,
		IReadOnlySet<Guid> accessibleStageIds)
	{
		if (assignedStages.Count == 0)
		{
			return null;
		}

		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return assignedStages.First();
		}

		var scopedStage = assignedStages.FirstOrDefault(accessibleStageIds.Contains);
		return scopedStage == Guid.Empty ? null : scopedStage;
	}

	public static void EnsureTenantWideDayLockAllowed(UserScopeContext scope)
	{
		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return;
		}

		throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
	}
}

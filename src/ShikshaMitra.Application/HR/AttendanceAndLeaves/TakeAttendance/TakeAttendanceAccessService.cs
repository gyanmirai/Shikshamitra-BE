using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR;
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

		hrScopeAccessService.EnsureHasConcreteHrAccess(scope, nameof(EnsureCanOperateAttendanceAsync));

		return scope;
	}

	public async Task EnsureEmployeeScopeAllowedAsync(UserScopeContext scope, Guid? employeeStageId)
	{
		await hrScopeAccessService.EnsureCanAccessEmployeeStageAsync(
			scope,
			employeeStageId,
			nameof(EnsureEmployeeScopeAllowedAsync));
	}

	public async Task<HashSet<Guid>> ResolveAccessibleStageIdsAsync(UserScopeContext scope) =>
		HrUserScopeAccess.HasHrWideAccess(scope)
			? []
			: (await hrScopeAccessService.ResolveAccessibleStageIdsAsync(scope)).ToHashSet();

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
		return scopedStage == Guid.Empty ? assignedStages.First() : scopedStage;
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

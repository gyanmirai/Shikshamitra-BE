using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeaveBalanceReader(
	IEmployeeLeaveRepository employeeLeaveRepository,
	IAcademicYearRepository academicYearRepository,
	ILeavePolicyRepository leavePolicyRepository,
	LeaveActorAccessService leaveActorAccessService)
	: ITransientDependency
{
	public async Task<LeaveBalanceListDto> GetBalancesAsync(Guid academicYearId, Guid? forEmployeeId)
	{
		await academicYearRepository.GetAsync(academicYearId);

		var actorContext = await leaveActorAccessService.ResolveContextAsync();
		var targetId = forEmployeeId ?? actorContext.ActorEmployeeId;
		if (!targetId.HasValue)
		{
			throw new BusinessException(HRErrorCodes.LeaveTargetEmployeeRequired);
		}

		await leaveActorAccessService.EnsureActorCanAccessEmployeeAsync(targetId.Value);

		var policies = await leavePolicyRepository.GetListByAcademicYearIdAsync(
			academicYearId,
			includeInactive: false);

		var items = new List<LeaveBalanceItemDto>();

		foreach (var policy in policies)
		{
			var used = await employeeLeaveRepository.GetApprovedLeaveDaysAsync(targetId.Value, policy.Id);
			var entitled = policy.LeaveCount;
			var remaining = Math.Max(0, entitled - used);

			items.Add(new LeaveBalanceItemDto
			{
				LeavePolicyId = policy.Id,
				PolicyName = policy.PolicyName,
				LeaveTypeMasterId = policy.LeaveTypeId,
				LeaveTypeName = policy.LeaveType?.Type ?? "",
				EntitledDays = entitled,
				UsedDays = used,
				RemainingDays = remaining
			});
		}

		return new LeaveBalanceListDto { Items = items };
	}
}

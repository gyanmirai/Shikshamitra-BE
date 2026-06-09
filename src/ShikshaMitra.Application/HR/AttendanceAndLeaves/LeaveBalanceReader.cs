using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeaveBalanceReader(
	IEmployeeLeaveRepository employeeLeaveRepository,
	IAcademicYearRepository academicYearRepository,
	LeaveActorAccessService leaveActorAccessService,
	HrPolicyResolver hrPolicyResolver)
	: ITransientDependency
{
	public async Task<LeaveBalanceListDto> GetBalancesAsync(Guid academicYearId, Guid? forEmployeeId)
	{
		var academicYear = await academicYearRepository.GetAsync(academicYearId);

		var actorContext = await leaveActorAccessService.ResolveContextAsync();
		var targetId = forEmployeeId ?? actorContext.ActorEmployeeId;
		if (!targetId.HasValue)
		{
			throw new BusinessException(HRErrorCodes.LeaveTargetEmployeeRequired);
		}

		await leaveActorAccessService.EnsureActorCanAccessEmployeeAsync(targetId.Value);

		var resolved = await hrPolicyResolver.ResolveAsync(
			targetId.Value,
			academicYear.StartDate.Date,
			academicYearId);
		var policy = resolved.LeavePolicy
			?? throw new BusinessException(HRErrorCodes.LeavePolicyNotFound);

		var items = new List<LeaveBalanceItemDto>();
		foreach (var rule in policy.Rules)
		{
			if (!rule.IsActive || rule.LeaveType == null || !rule.LeaveType.IsActive)
			{
				continue;
			}

			var used = await employeeLeaveRepository.GetApprovedLeaveDaysByRuleAsync(targetId.Value, rule.Id);
			var remaining = rule.AllowNegativeBalance ? decimal.MaxValue : Math.Max(0, rule.LeaveCount - used);

			items.Add(new LeaveBalanceItemDto
			{
				LeavePolicyId = policy.Id,
				LeavePolicyRuleId = rule.Id,
				PolicyName = policy.PolicyName,
				LeaveTypeMasterId = rule.LeaveTypeId,
				LeaveTypeName = rule.LeaveType.Type,
				EntitledDays = rule.LeaveCount,
				UsedDays = used,
				RemainingDays = remaining
			});
		}

		return new LeaveBalanceListDto { Items = items };
	}
}

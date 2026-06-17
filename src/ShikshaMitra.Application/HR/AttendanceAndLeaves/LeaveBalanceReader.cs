using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
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

		var activeRules = policy.Rules
			.Where(rule => rule.IsActive)
			.ToList();
		var unitsByPaidType = await employeeLeaveRepository.GetBalanceUnitsByPolicyAndPaidTypeAsync(
			targetId.Value,
			policy.Id,
			activeRules.Select(rule => rule.PaidType).ToList());

		var items = new List<LeaveBalanceItemDto>();
		foreach (var rule in activeRules)
		{
			unitsByPaidType.TryGetValue(rule.PaidType, out var units);
			var used = units?.ApprovedDays ?? 0;
			var pending = units?.PendingDays ?? 0;
			var remaining = rule.IsUnlimited || rule.AllowNegativeBalance
				? null
				: (decimal?)Math.Max(0, (rule.LeaveCount ?? 0) - used);

			items.Add(new LeaveBalanceItemDto
			{
				LeavePolicyId = policy.Id,
				LeavePolicyRuleId = rule.Id,
				PolicyName = policy.PolicyName,
				PaidType = LeavePolicyProjection.ToPaidType(rule.PaidType),
				EntitlementMode = LeavePolicyProjection.ToEntitlementMode(rule.EntitlementMode),
				EntitledDays = rule.LeaveCount,
				UsedDays = used,
				PendingDays = pending,
				RemainingDays = remaining,
				IsUnlimited = rule.IsUnlimited,
				AllowNegativeBalance = rule.AllowNegativeBalance
			});
		}

		return new LeaveBalanceListDto { Items = items };
	}
}

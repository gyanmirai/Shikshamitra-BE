using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.AttendanceAndLeaves.YearEndSettlements;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Timing;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeaveBalanceReader(
	LeaveBalanceUnitService leaveBalanceUnitService,
	IEmployeeRepository employeeRepository,
	IAcademicYearRepository academicYearRepository,
	LeaveActorAccessService leaveActorAccessService,
	HrPolicyResolver hrPolicyResolver,
	LeaveCarryForwardBalanceReader carryForwardBalanceReader,
	IClock clock)
	: ITransientDependency
{
	public async Task<LeaveBalanceListDto> GetBalancesAsync(Guid academicYearId, Guid? forEmployeeId)
	{
		var actorContext = await leaveActorAccessService.ResolveContextAsync();
		var targetId = forEmployeeId ?? actorContext.ActorEmployeeId;
		if (!targetId.HasValue)
		{
			throw new BusinessException(HRErrorCodes.LeaveTargetEmployeeRequired);
		}

		await leaveActorAccessService.EnsureActorCanAccessEmployeeAsync(targetId.Value);
		return await GetBalancesForAuthorizedEmployeeAsync(academicYearId, targetId.Value);
	}

	/// <summary>
	/// Calculates balances after the calling workflow has authorized access to the employee.
	/// Keep public leave endpoints on <see cref="GetBalancesAsync"/> so their actor hierarchy rules remain enforced.
	/// </summary>
	internal async Task<LeaveBalanceListDto> GetBalancesForAuthorizedEmployeeAsync(
		Guid academicYearId,
		Guid employeeId)
	{
		var academicYear = await academicYearRepository.GetAsync(academicYearId);
		var targetId = employeeId;
		var targetEmployee = await employeeRepository.GetAsync(targetId);

		var resolved = await hrPolicyResolver.ResolveAsync(
			targetId,
			academicYear.StartDate.Date,
			academicYearId);
		var policy = resolved.LeavePolicy
			?? throw new BusinessException(HRErrorCodes.LeavePolicyNotFound);

		var activeRules = policy.Rules
			.Where(rule => rule.IsActive)
			.ToList();
		var unitsByPaidType = await leaveBalanceUnitService.GetBalanceUnitsByPolicyAndPaidTypeAsync(
			targetId,
			policy.Id,
			activeRules.Select(rule => rule.PaidType).ToList());
		var carryInByRule = await carryForwardBalanceReader.GetCarryInByRuleAsync(
			targetId,
			academicYearId,
			activeRules.Select(rule => rule.Id).ToList());

		var items = new List<LeaveBalanceItemDto>();
		foreach (var rule in activeRules)
		{
			unitsByPaidType.TryGetValue(rule.PaidType, out var units);
			var used = units?.ApprovedDays ?? 0;
			var pending = units?.PendingDays ?? 0;
			carryInByRule.TryGetValue(rule.Id, out var carryIn);
			var accrued = LeaveAccrualCalculator.CalculateAccruedDays(
				policy,
				rule,
				academicYear.StartDate,
				academicYear.EndDate,
				targetEmployee.DateOfJoining,
				clock.Now.Date);
			var remaining = rule.IsUnlimited
				? null
				: (decimal?)((accrued ?? 0m) + carryIn - used);

			items.Add(new LeaveBalanceItemDto
			{
				LeavePolicyId = policy.Id,
				LeavePolicyRuleId = rule.Id,
				PolicyName = policy.PolicyName,
				PaidType = LeavePolicyProjection.ToPaidType(rule.PaidType),
				EntitlementMode = LeavePolicyProjection.ToEntitlementMode(rule.EntitlementMode),
				EntitledDays = rule.LeaveCount,
				AccruedDays = accrued,
				CarryForwardDays = carryIn,
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

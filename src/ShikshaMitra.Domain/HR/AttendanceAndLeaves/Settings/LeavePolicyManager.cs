using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// Domain service for managing employee leave policies with business logic
/// </summary>
public class LeavePolicyManager : DomainService
{
	private readonly ILeavePolicyRepository _leavePolicyRepository;

	public LeavePolicyManager(ILeavePolicyRepository leavePolicyRepository)
	{
		_leavePolicyRepository = leavePolicyRepository;
	}

	public async Task<LeavePolicy> CreateAsync(
		string policyName,
		string leaveType,
		int maximumDaysPerYear,
		LeavePaidType paidType,
		bool carryForward,
		bool encashable,
		bool requiresApproval,
		string? description = null)
	{
		// Check if policy name already exists
		await CheckPolicyNameAvailabilityAsync(policyName);

		var leavePolicy = new LeavePolicy(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			policyName,
			leaveType,
			maximumDaysPerYear,
			paidType,
			carryForward,
			encashable,
			requiresApproval,
			description
		);

		return leavePolicy;
	}

	public async Task UpdateAsync(
		LeavePolicy leavePolicy,
		string policyName,
		string leaveType,
		int maximumDaysPerYear,
		LeavePaidType paidType,
		bool carryForward,
		bool encashable,
		bool requiresApproval,
		string? description)
	{
		// Check if policy name already exists (excluding current policy)
		if (leavePolicy.PolicyName != policyName)
		{
			await CheckPolicyNameAvailabilityAsync(policyName, leavePolicy.Id);
		}

		leavePolicy.Update(
			policyName,
			leaveType,
			maximumDaysPerYear,
			paidType,
			carryForward,
			encashable,
			requiresApproval,
			description
		);
	}

	private async Task CheckPolicyNameAvailabilityAsync(string policyName, Guid? excludeId = null)
	{
		if (await _leavePolicyRepository.IsPolicyNameExistsAsync(policyName, excludeId))
		{
			throw new BusinessException("ShikshaMitra:LeavePolicyNameAlreadyExists")
				.WithData("policyName", policyName);
		}
	}
}

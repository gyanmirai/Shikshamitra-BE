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
	private readonly ILeaveTypeRepository _leaveTypeRepository;

	public LeavePolicyManager(
		ILeavePolicyRepository leavePolicyRepository,
		ILeaveTypeRepository leaveTypeRepository)
	{
		_leavePolicyRepository = leavePolicyRepository;
		_leaveTypeRepository = leaveTypeRepository;
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

		// Validate that the referenced LeaveType exists
		await ValidateLeaveTypeExistsAsync(leaveType);

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

		// Validate that the referenced LeaveType exists
		await ValidateLeaveTypeExistsAsync(leaveType);

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

	/// <summary>
	/// Validates that the specified LeaveType exists in the database.
	/// Throws a clear exception if the LeaveType is not found.
	/// </summary>
	/// <param name="leaveType">The LeaveType name to validate</param>
	/// <exception cref="BusinessException">Thrown when the LeaveType does not exist</exception>
	private async Task ValidateLeaveTypeExistsAsync(string leaveType)
	{
		Check.NotNullOrWhiteSpace(leaveType, nameof(leaveType));

		var existingLeaveType = await _leaveTypeRepository.FindByTypeAsync(leaveType);
		
		if (existingLeaveType == null)
		{
			throw new BusinessException("ShikshaMitra:LeaveTypeNotFound")
				.WithData("leaveType", leaveType)
				.WithData("message", $"The leave type '{leaveType}' does not exist. Please create the leave type first before creating a policy for it.");
		}

		// Validate that the LeaveType is active
		if (!existingLeaveType.IsActive)
		{
			throw new BusinessException("ShikshaMitra:LeaveTypeInactive")
				.WithData("leaveType", leaveType)
				.WithData("message", $"The leave type '{leaveType}' is inactive. Please activate it before creating a policy for it.");
		}
	}
}

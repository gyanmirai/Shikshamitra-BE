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
		Guid educationMediumStageId,
		string policyName,
		Guid leaveTypeId,
		int maximumDaysPerYear,
		LeavePaidType paidType,
		bool carryForward,
		bool encashable,
		bool requiresApproval,
		string? description = null)
	{
		// Check if policy name already exists within the organization
		await CheckPolicyNameAvailabilityAsync(educationMediumStageId, policyName);

		// Validate that the referenced LeaveType exists and is active
		await ValidateLeaveTypeExistsAsync(leaveTypeId);

		var leavePolicy = new LeavePolicy(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			educationMediumStageId,
			policyName,
			leaveTypeId,
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
		Guid leaveTypeId,
		int maximumDaysPerYear,
		LeavePaidType paidType,
		bool carryForward,
		bool encashable,
		bool requiresApproval,
		string? description)
	{
		// Check if policy name already exists (excluding current policy) within the same organization
		if (leavePolicy.PolicyName != policyName)
		{
			await CheckPolicyNameAvailabilityAsync(leavePolicy.EducationMediumStageId, policyName, leavePolicy.Id);
		}

		// Validate that the referenced LeaveType exists and is active
		await ValidateLeaveTypeExistsAsync(leaveTypeId);

		leavePolicy.Update(
			policyName,
			leaveTypeId,
			maximumDaysPerYear,
			paidType,
			carryForward,
			encashable,
			requiresApproval,
			description
		);
	}

	private async Task CheckPolicyNameAvailabilityAsync(Guid educationMediumStageId, string policyName, Guid? excludeId = null)
	{
		if (await _leavePolicyRepository.IsPolicyNameExistsAsync(educationMediumStageId, policyName, excludeId))
		{
			throw new BusinessException("ShikshaMitra:LeavePolicyNameAlreadyExists")
				.WithData("policyName", policyName);
		}
	}

	/// <summary>
	/// Validates that the specified LeaveType exists and is active.
	/// </summary>
	/// <param name="leaveTypeId">The LeaveType ID to validate</param>
	/// <exception cref="BusinessException">Thrown when the LeaveType does not exist or is inactive</exception>
	private async Task ValidateLeaveTypeExistsAsync(Guid leaveTypeId)
	{
		var existingLeaveType = await _leaveTypeRepository.GetAsync(leaveTypeId);

		if (existingLeaveType == null)
		{
			throw new BusinessException("ShikshaMitra:LeaveTypeNotFound")
				.WithData("leaveTypeId", leaveTypeId)
				.WithData("message", $"The leave type with ID '{leaveTypeId}' does not exist. Please create the leave type first.");
		}

		// Validate that the LeaveType is active
		if (!existingLeaveType.IsActive)
		{
			throw new BusinessException("ShikshaMitra:LeaveTypeInactive")
				.WithData("leaveTypeId", leaveTypeId)
				.WithData("message", $"The leave type with ID '{leaveTypeId}' is inactive. Please activate it before creating a policy for it.");
		}
	}
}

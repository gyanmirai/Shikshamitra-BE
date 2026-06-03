using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

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
		Guid academicYearId,
		string policyName,
		Guid leaveTypeId,
		LeaveAccrualPolicyType accrualPolicyType,
		int leaveCount,
		LeavePaidType paidType,
		bool carryForward,
		bool encashable,
		LeaveEncashmentTiming? encashmentTiming,
		bool requiresApproval,
		string? description = null)
	{
		await CheckPolicyNameAvailabilityAsync(academicYearId, policyName);
		await ValidateLeaveTypeExistsAsync(leaveTypeId, academicYearId);

		return new LeavePolicy(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			policyName,
			leaveTypeId,
			accrualPolicyType,
			leaveCount,
			paidType,
			carryForward,
			encashable,
			encashmentTiming,
			requiresApproval,
			description
		);
	}

	public async Task UpdateAsync(
		LeavePolicy leavePolicy,
		string policyName,
		Guid leaveTypeId,
		LeaveAccrualPolicyType accrualPolicyType,
		int leaveCount,
		LeavePaidType paidType,
		bool carryForward,
		bool encashable,
		LeaveEncashmentTiming? encashmentTiming,
		bool requiresApproval,
		string? description)
	{
		if (!string.Equals(leavePolicy.PolicyName, policyName, StringComparison.OrdinalIgnoreCase))
		{
			await CheckPolicyNameAvailabilityAsync(leavePolicy.AcademicYearId, policyName, leavePolicy.Id);
		}

		await ValidateLeaveTypeExistsAsync(leaveTypeId, leavePolicy.AcademicYearId);

		leavePolicy.Update(
			policyName,
			leaveTypeId,
			accrualPolicyType,
			leaveCount,
			paidType,
			carryForward,
			encashable,
			encashmentTiming,
			requiresApproval,
			description);
	}

	private async Task CheckPolicyNameAvailabilityAsync(Guid academicYearId, string policyName, Guid? excludeId = null)
	{
		if (await _leavePolicyRepository.IsPolicyNameExistsAsync(academicYearId, policyName, excludeId))
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyNameAlreadyExists)
				.WithData("policyName", policyName);
		}
	}

	private async Task ValidateLeaveTypeExistsAsync(Guid leaveTypeId, Guid academicYearId)
	{
		var existingLeaveType = await _leaveTypeRepository.GetAsync(leaveTypeId);

		if (existingLeaveType == null)
		{
			throw new BusinessException(HRErrorCodes.LeaveTypeNotFound)
				.WithData("leaveTypeId", leaveTypeId);
		}

		if (!existingLeaveType.IsActive)
		{
			throw new BusinessException(HRErrorCodes.LeaveTypeInactive)
				.WithData("leaveTypeId", leaveTypeId);
		}

		if (existingLeaveType.AcademicYearId != academicYearId)
		{
			throw new BusinessException(HRErrorCodes.LeaveTypeAcademicYearMismatch)
				.WithData("leaveTypeId", leaveTypeId)
				.WithData("academicYearId", academicYearId);
		}
	}
}

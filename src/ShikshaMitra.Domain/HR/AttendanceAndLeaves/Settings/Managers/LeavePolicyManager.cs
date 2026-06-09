using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Policies;
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
		DateTime effectiveFrom,
		DateTime? effectiveTo,
		HrPolicyStatus status,
		IEnumerable<LeavePolicyRuleInput> rules,
		string? description = null)
	{
		await CheckPolicyNameAvailabilityAsync(academicYearId, policyName);

		var policy = new LeavePolicy(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			policyName,
			effectiveFrom,
			effectiveTo,
			status,
			description
		);

		policy.ReplaceRules(await BuildRulesAsync(policy.Id, academicYearId, rules));
		return policy;
	}

	public async Task UpdateAsync(
		LeavePolicy leavePolicy,
		string policyName,
		DateTime effectiveFrom,
		DateTime? effectiveTo,
		HrPolicyStatus status,
		IEnumerable<LeavePolicyRuleInput> rules,
		string? description)
	{
		if (!string.Equals(leavePolicy.PolicyName, policyName, StringComparison.OrdinalIgnoreCase))
		{
			await CheckPolicyNameAvailabilityAsync(leavePolicy.AcademicYearId, policyName, leavePolicy.Id);
		}

		leavePolicy.Update(
			policyName,
			effectiveFrom,
			effectiveTo,
			status,
			description);
		leavePolicy.ReplaceRules(await BuildRulesAsync(leavePolicy.Id, leavePolicy.AcademicYearId, rules));
	}

	private async Task CheckPolicyNameAvailabilityAsync(Guid academicYearId, string policyName, Guid? excludeId = null)
	{
		if (await _leavePolicyRepository.IsPolicyNameExistsAsync(academicYearId, policyName, excludeId))
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyNameAlreadyExists)
				.WithData("policyName", policyName);
		}
	}

	private async Task<List<LeavePolicyRule>> BuildRulesAsync(
		Guid leavePolicyId,
		Guid academicYearId,
		IEnumerable<LeavePolicyRuleInput> rules)
	{
		var materialized = rules.ToList();
		if (materialized.Count == 0)
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyRuleNotFound);
		}

		var duplicate = materialized
			.Where(rule => rule.IsActive)
			.GroupBy(rule => rule.LeaveTypeId)
			.FirstOrDefault(group => group.Count() > 1);
		if (duplicate != null)
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyDuplicateLeaveTypeRule)
				.WithData("leaveTypeId", duplicate.Key);
		}

		var result = new List<LeavePolicyRule>();
		foreach (var input in materialized)
		{
			await ValidateLeaveTypeExistsAsync(input.LeaveTypeId, academicYearId);
			result.Add(new LeavePolicyRule(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				leavePolicyId,
				input.LeaveTypeId,
				input.AccrualPolicyType,
				input.LeaveCount,
				input.PaidType,
				input.CarryForward,
				input.CarryForwardLimit,
				input.Encashable,
				input.EncashmentTiming,
				input.RequiresApproval,
				input.MinNoticeDays,
				input.MaxDaysPerRequest,
				input.AllowHalfDay,
				input.AttachmentRequiredAfterDays,
				input.AllowNegativeBalance,
				input.IsActive));
		}

		return result;
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

public sealed record LeavePolicyRuleInput(
	Guid LeaveTypeId,
	LeaveAccrualPolicyType AccrualPolicyType,
	int LeaveCount,
	LeavePaidType PaidType,
	bool CarryForward,
	int? CarryForwardLimit,
	bool Encashable,
	LeaveEncashmentTiming? EncashmentTiming,
	bool RequiresApproval,
	int MinNoticeDays,
	int MaxDaysPerRequest,
	bool AllowHalfDay,
	int? AttachmentRequiredAfterDays,
	bool AllowNegativeBalance,
	bool IsActive);

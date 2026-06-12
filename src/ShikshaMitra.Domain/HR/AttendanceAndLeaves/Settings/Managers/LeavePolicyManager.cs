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
		bool isDefault,
		IEnumerable<LeavePolicyRuleInput> rules,
		string? description = null)
	{
		await CheckPolicyNameAvailabilityAsync(academicYearId, policyName);
		var defaultAfterCreate = await ResolveDefaultAfterCreateAsync(academicYearId, status, isDefault);
		if (defaultAfterCreate)
		{
			await ClearDefaultsAsync(academicYearId);
		}

		var policy = new LeavePolicy(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			policyName,
			effectiveFrom,
			effectiveTo,
			status,
			defaultAfterCreate,
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
		bool isDefault,
		IEnumerable<LeavePolicyRuleInput> rules,
		string? description)
	{
		if (!string.Equals(leavePolicy.PolicyName, policyName, StringComparison.OrdinalIgnoreCase))
		{
			await CheckPolicyNameAvailabilityAsync(leavePolicy.AcademicYearId, policyName, leavePolicy.Id);
		}

		await EnsureDefaultCanChangeAsync(leavePolicy, isDefault, status);
		if (isDefault)
		{
			await ClearDefaultsAsync(leavePolicy.AcademicYearId, leavePolicy.Id);
		}

		leavePolicy.Update(
			policyName,
			effectiveFrom,
			effectiveTo,
			status,
			isDefault,
			description);
		leavePolicy.ReplaceRules(await BuildRulesAsync(leavePolicy.Id, leavePolicy.AcademicYearId, rules));
	}

	public async Task EnsureCanDeactivateAsync(LeavePolicy leavePolicy)
	{
		await EnsureDefaultCanChangeAsync(leavePolicy, isDefaultAfterUpdate: false, HrPolicyStatus.Archived);
	}

	public async Task SetDefaultAsync(LeavePolicy leavePolicy)
	{
		if (leavePolicy.Status != HrPolicyStatus.Active || !leavePolicy.IsActive)
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyInactive);
		}

		await ClearDefaultsAsync(leavePolicy.AcademicYearId, leavePolicy.Id);
		leavePolicy.SetDefault(true);
	}

	private async Task CheckPolicyNameAvailabilityAsync(Guid academicYearId, string policyName, Guid? excludeId = null)
	{
		if (await _leavePolicyRepository.IsPolicyNameExistsAsync(academicYearId, policyName, excludeId))
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyNameAlreadyExists)
				.WithData("policyName", policyName);
		}
	}

	private async Task<bool> ResolveDefaultAfterCreateAsync(Guid academicYearId, HrPolicyStatus status, bool requestedDefault)
	{
		if (requestedDefault && status != HrPolicyStatus.Active)
		{
			throw new BusinessException(HRErrorCodes.DefaultPolicyMustBeActive);
		}

		if (status != HrPolicyStatus.Active)
		{
			return false;
		}

		return requestedDefault || !await _leavePolicyRepository.HasActiveDefaultAsync(academicYearId);
	}

	private async Task EnsureDefaultCanChangeAsync(
		LeavePolicy leavePolicy,
		bool isDefaultAfterUpdate,
		HrPolicyStatus statusAfterUpdate)
	{
		if (isDefaultAfterUpdate && statusAfterUpdate != HrPolicyStatus.Active)
		{
			throw new BusinessException(HRErrorCodes.DefaultPolicyMustBeActive);
		}

		if (!leavePolicy.IsDefault || (isDefaultAfterUpdate && statusAfterUpdate == HrPolicyStatus.Active))
		{
			return;
		}

		if (!await _leavePolicyRepository.HasActiveDefaultAsync(leavePolicy.AcademicYearId, leavePolicy.Id))
		{
			throw new BusinessException(HRErrorCodes.DefaultLeavePolicyRequired);
		}
	}

	private async Task ClearDefaultsAsync(Guid academicYearId, Guid? excludeId = null)
	{
		var defaults = await _leavePolicyRepository.GetActiveDefaultsAsync(academicYearId, excludeId);
		foreach (var item in defaults)
		{
			item.SetDefault(false);
			await _leavePolicyRepository.UpdateAsync(item, autoSave: false);
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

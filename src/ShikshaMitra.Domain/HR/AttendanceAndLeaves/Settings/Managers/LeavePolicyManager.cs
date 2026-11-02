using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Policies;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class LeavePolicyManager : DomainService
{
	private readonly ILeavePolicyRepository _leavePolicyRepository;
	private readonly IEmployeeLeaveRepository _employeeLeaveRepository;
	private readonly IRepository<HrPolicyAssignment, Guid> _assignmentRepository;
	private readonly ILeavePolicyReferenceLock _referenceLock;

	public LeavePolicyManager(
		ILeavePolicyRepository leavePolicyRepository,
		IEmployeeLeaveRepository employeeLeaveRepository,
		IRepository<HrPolicyAssignment, Guid> assignmentRepository,
		ILeavePolicyReferenceLock referenceLock)
	{
		_leavePolicyRepository = leavePolicyRepository;
		_employeeLeaveRepository = employeeLeaveRepository;
		_assignmentRepository = assignmentRepository;
		_referenceLock = referenceLock;
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
		await EnsurePolicyNameAvailableAsync(academicYearId, policyName);
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

		policy.ReplaceRules(BuildRules(policy.Id, rules));
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
		if (!string.Equals(leavePolicy.PolicyName, policyName, StringComparison.OrdinalIgnoreCase) || (leavePolicy.Status == HrPolicyStatus.Archived && status != HrPolicyStatus.Archived))
		{
			await EnsurePolicyNameAvailableAsync(leavePolicy.AcademicYearId, policyName, leavePolicy.Id);
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
		var referencedRuleIds = await _employeeLeaveRepository.GetReferencedRuleIdsAsync(
			leavePolicy.Rules.Select(rule => rule.Id).ToList());
		leavePolicy.SyncRules(
			BuildRules(leavePolicy.Id, rules),
			referencedRuleIds);
	}

	public async Task EnsureCanDeactivateAsync(LeavePolicy leavePolicy)
	{
		await _referenceLock.AcquireAsync(leavePolicy.Id, CurrentTenant.Id);
		await EnsureDefaultCanChangeAsync(leavePolicy, isDefaultAfterUpdate: false, HrPolicyStatus.Archived);
		var today = Clock.Now.Date;
		var assignments = await _assignmentRepository.GetQueryableAsync();
		var referenceCount = await AsyncExecuter.CountAsync(assignments.Where(assignment =>
			assignment.TenantId == CurrentTenant.Id &&
			assignment.AcademicYearId == leavePolicy.AcademicYearId &&
			assignment.LeavePolicyId == leavePolicy.Id &&
			assignment.IsActive &&
			(!assignment.EffectiveTo.HasValue || assignment.EffectiveTo.Value >= today)));
		if (referenceCount > 0)
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyHasActiveAssignments)
				.WithData("policyId", leavePolicy.Id)
				.WithData("assignmentCount", referenceCount);
		}
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

	public async Task EnsurePolicyNameAvailableAsync(Guid academicYearId, string policyName, Guid? excludeId = null)
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

	private List<LeavePolicyRule> BuildRules(
		Guid leavePolicyId,
		IEnumerable<LeavePolicyRuleInput> rules)
	{
		var materialized = rules.ToList();
		if (materialized.Count(rule => rule.IsActive) != 2 ||
			materialized.All(rule => !rule.IsActive || rule.PaidType != LeavePaidType.Paid) ||
			materialized.All(rule => !rule.IsActive || rule.PaidType != LeavePaidType.Unpaid))
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyPaidUnpaidRulesRequired);
		}

		var duplicate = materialized
			.Where(rule => rule.IsActive)
			.GroupBy(rule => rule.PaidType)
			.FirstOrDefault(group => group.Count() > 1);
		if (duplicate != null)
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyDuplicatePaidTypeRule)
				.WithData("paidType", duplicate.Key);
		}

		var result = new List<LeavePolicyRule>();
		foreach (var input in materialized)
		{
			result.Add(new LeavePolicyRule(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				leavePolicyId,
				input.PaidType,
				input.EntitlementMode,
				input.AccrualPolicyType,
				input.LeaveCount,
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
				input.IsActive, input.PayrollCategory));
		}

		return result;
	}
}

public sealed record LeavePolicyRuleInput(
	LeavePaidType PaidType,
	LeaveEntitlementMode EntitlementMode,
	LeaveAccrualPolicyType AccrualPolicyType,
	int? LeaveCount,
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
	bool IsActive, string PayrollCategory = "PAID");
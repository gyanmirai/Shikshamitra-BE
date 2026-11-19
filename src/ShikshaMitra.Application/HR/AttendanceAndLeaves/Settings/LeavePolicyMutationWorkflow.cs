using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Policies;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

[RemoteService(false)]
public class LeavePolicyMutationWorkflow(
	IRepository<LeavePolicy, Guid> repository,
	ILeavePolicyRepository leavePolicyRepository,
	LeavePolicyManager leavePolicyManager,
	HrPolicyNameUniquenessService nameUniquenessService,
	LeavePolicyLifecycle lifecycle,
	LeavePolicyMutationReview review,
	LeavePolicyCacheService cacheService)
	: ApplicationService, ITransientDependency
{
	public async Task<LeavePolicyResponse> CreateLeavePolicyAsync(Guid academicYearId, LeavePolicyCreateRequest input)
	{
		var lifecycleContext = await lifecycle.BeginAsync(null, academicYearId, changesDefault: input.IsDefault);
		// Initial unreferenced creation must remain possible when a year has no default.
		// Replacing an existing default is a reviewed mutation, including creation.
		if (lifecycleContext.Policies.Count > 0) await review.EnsureAsync(academicYearId, null, "create", input);
		var status = LeavePolicyProjection.ParseStatus(input.Status);
		HrPolicyStatusGuard.EnsureArchiveUsesArchiveWorkflow(status);
		var policy = await leavePolicyManager.CreateAsync(
			academicYearId,
			input.PolicyTitle,
			input.EffectiveFrom,
			input.EffectiveTo,
			status,
			input.IsDefault,
			input.Rules.Select(LeavePolicyProjection.ToRuleInput),
			input.Description);

		await nameUniquenessService.ExecuteLeaveWriteAsync(
			policy.PolicyName,
			() => repository.InsertAsync(policy, autoSave: true));
		await ClearPolicyCachesAsync(academicYearId);
		await lifecycle.CompleteAsync(lifecycleContext, policy, "created", input.ChangeReason);

		policy = await leavePolicyRepository.GetWithRulesAsync(policy.Id) ?? policy;
		return LeavePolicyProjection.ToCreateResponse(policy);
	}

	public async Task<LeavePolicyDetailResponse> UpdateLeavePolicyAsync(Guid policyId, LeavePolicyUpdateRequest input)
	{
		var lifecycleContext = await lifecycle.BeginAsync(policyId, changesDefault: input.IsDefault);
		var policy = await leavePolicyRepository.GetWithRulesAsync(policyId);
		if (policy == null)
		{
			throw new EntityNotFoundException(typeof(LeavePolicy), policyId);
		}

		HrPolicyStatusGuard.EnsureEditable(policy.Status);
		if (input.ExpectedVersion.HasValue && input.ExpectedVersion.Value != policy.Version)
			throw new BusinessException(HRErrorCodes.HrPolicyImpactConfirmationInvalid);
		var status = LeavePolicyProjection.ParseStatus(input.Status);
		HrPolicyStatusGuard.EnsureArchiveUsesArchiveWorkflow(status);
		await review.EnsureAsync(policy.AcademicYearId, policy, "update", input);
		await leavePolicyManager.UpdateAsync(
			policy,
			input.PolicyTitle,
			input.EffectiveFrom,
			input.EffectiveTo,
			status,
			input.IsDefault,
			input.Rules.Select(LeavePolicyProjection.ToRuleInput),
			input.Description);

		await nameUniquenessService.ExecuteLeaveWriteAsync(
			policy.PolicyName,
			() => repository.UpdateAsync(policy, autoSave: true));
		await ClearPolicyCachesAsync(policy.AcademicYearId);
		await lifecycle.CompleteAsync(lifecycleContext, policy, "edited", input.ChangeReason);

		policy = await leavePolicyRepository.GetWithRulesAsync(policyId) ?? policy;
		return LeavePolicyProjection.ToDetail(policy);
	}

	public async Task DeactivateAsync(Guid policyId)
	{
		var lifecycleContext = await lifecycle.BeginAsync(policyId);
		var policy = await repository.GetAsync(policyId);
		await leavePolicyManager.EnsureCanDeactivateAsync(policy);
		await review.EnsureAsync(policy.AcademicYearId, policy, "archive");
		policy.Deactivate();
		await repository.UpdateAsync(policy, autoSave: true);
		await ClearPolicyCachesAsync(policy.AcademicYearId);
		await lifecycle.CompleteAsync(lifecycleContext, policy, "archived");
	}

	public async Task<LeavePolicyDetailResponse> UnarchiveAsync(Guid policyId)
	{
		var lifecycleContext = await lifecycle.BeginAsync(policyId);
		var policy = await leavePolicyRepository.GetWithRulesAsync(policyId);
		if (policy == null)
		{
			throw new EntityNotFoundException(typeof(LeavePolicy), policyId);
		}

		if (policy.Status != HrPolicyStatus.Archived)
		{
			return LeavePolicyProjection.ToDetail(policy);
		}

		await leavePolicyManager.EnsurePolicyNameAvailableAsync(
			policy.AcademicYearId,
			policy.PolicyName,
			policy.Id);
		policy.Unarchive();
		await lifecycle.CompleteAsync(lifecycleContext, policy, "unarchived");
		await nameUniquenessService.ExecuteLeaveWriteAsync(
			policy.PolicyName,
			() => repository.UpdateAsync(policy, autoSave: true));
		await ClearPolicyCachesAsync(policy.AcademicYearId);

		policy = await leavePolicyRepository.GetWithRulesAsync(policyId) ?? policy;
		return LeavePolicyProjection.ToDetail(policy);
	}

	public async Task<LeavePolicyDetailResponse> SetDefaultAsync(Guid policyId)
	{
		var lifecycleContext = await lifecycle.BeginAsync(policyId, changesDefault: true);
		var policy = await leavePolicyRepository.GetWithRulesAsync(policyId);
		if (policy == null)
		{
			throw new EntityNotFoundException(typeof(LeavePolicy), policyId);
		}

		if (policy.Status != HrPolicyStatus.Active || !policy.IsActive) throw new BusinessException(HRErrorCodes.LeavePolicyInactive);
		await review.EnsureAsync(policy.AcademicYearId, policy, "set-default");
		await leavePolicyManager.SetDefaultAsync(policy);
		await lifecycle.CompleteAsync(lifecycleContext, policy, "set-default");
		await repository.UpdateAsync(policy, autoSave: true);
		await ClearPolicyCachesAsync(policy.AcademicYearId);

		policy = await leavePolicyRepository.GetWithRulesAsync(policyId) ?? policy;
		return LeavePolicyProjection.ToDetail(policy);
	}

	public async Task<LeavePolicyDeleteResponse> DeleteLeavePolicyAsync(Guid policyId)
	{
		await DeactivateAsync(policyId);

		return new LeavePolicyDeleteResponse
		{
			Id = policyId.ToString(),
			Message = "Leave policy deleted successfully",
			DeletedAt = Clock.Now
		};
	}

	private async Task ClearPolicyCachesAsync(Guid academicYearId)
	{
		await cacheService.ClearActiveAsync();
		await cacheService.ClearContractAsync(academicYearId);
	}
}

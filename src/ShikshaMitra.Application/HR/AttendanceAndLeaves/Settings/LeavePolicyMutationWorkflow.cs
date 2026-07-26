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
	LeavePolicyCacheService cacheService)
	: ApplicationService, ITransientDependency
{
	public async Task<LeavePolicyResponse> CreateLeavePolicyAsync(Guid academicYearId, LeavePolicyCreateRequest input)
	{
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

		policy = await leavePolicyRepository.GetWithRulesAsync(policy.Id) ?? policy;
		return LeavePolicyProjection.ToCreateResponse(policy);
	}

	public async Task<LeavePolicyDetailResponse> UpdateLeavePolicyAsync(Guid policyId, LeavePolicyUpdateRequest input)
	{
		var policy = await leavePolicyRepository.GetWithRulesAsync(policyId);
		if (policy == null)
		{
			throw new EntityNotFoundException(typeof(LeavePolicy), policyId);
		}

		HrPolicyStatusGuard.EnsureEditable(policy.Status);
		var status = LeavePolicyProjection.ParseStatus(input.Status);
		HrPolicyStatusGuard.EnsureArchiveUsesArchiveWorkflow(status);
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

		policy = await leavePolicyRepository.GetWithRulesAsync(policyId) ?? policy;
		return LeavePolicyProjection.ToDetail(policy);
	}

	public async Task DeactivateAsync(Guid policyId)
	{
		var policy = await repository.GetAsync(policyId);
		await leavePolicyManager.EnsureCanDeactivateAsync(policy);
		policy.Deactivate();
		await repository.UpdateAsync(policy, autoSave: true);
		await ClearPolicyCachesAsync(policy.AcademicYearId);
	}

	public async Task<LeavePolicyDetailResponse> UnarchiveAsync(Guid policyId)
	{
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
		await nameUniquenessService.ExecuteLeaveWriteAsync(
			policy.PolicyName,
			() => repository.UpdateAsync(policy, autoSave: true));
		await ClearPolicyCachesAsync(policy.AcademicYearId);

		policy = await leavePolicyRepository.GetWithRulesAsync(policyId) ?? policy;
		return LeavePolicyProjection.ToDetail(policy);
	}

	public async Task<LeavePolicyDetailResponse> SetDefaultAsync(Guid policyId)
	{
		var policy = await leavePolicyRepository.GetWithRulesAsync(policyId);
		if (policy == null)
		{
			throw new EntityNotFoundException(typeof(LeavePolicy), policyId);
		}

		await leavePolicyManager.SetDefaultAsync(policy);
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

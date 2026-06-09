using System;
using System.Linq;
using System.Threading.Tasks;
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
	LeavePolicyCacheService cacheService)
	: ApplicationService, ITransientDependency
{
	public async Task<LeavePolicyResponse> CreateLeavePolicyAsync(Guid academicYearId, LeavePolicyCreateRequest input)
	{
		var policy = await leavePolicyManager.CreateAsync(
			academicYearId,
			input.PolicyTitle,
			input.EffectiveFrom,
			input.EffectiveTo,
			LeavePolicyProjection.ParseStatus(input.Status),
			input.Rules.Select(LeavePolicyProjection.ToRuleInput),
			input.Description);

		await repository.InsertAsync(policy, autoSave: true);
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

		await leavePolicyManager.UpdateAsync(
			policy,
			input.PolicyTitle,
			input.EffectiveFrom,
			input.EffectiveTo,
			LeavePolicyProjection.ParseStatus(input.Status),
			input.Rules.Select(LeavePolicyProjection.ToRuleInput),
			input.Description);

		await repository.UpdateAsync(policy, autoSave: true);
		await ClearPolicyCachesAsync(policy.AcademicYearId);

		policy = await leavePolicyRepository.GetWithRulesAsync(policyId) ?? policy;
		return LeavePolicyProjection.ToDetail(policy);
	}

	public async Task DeactivateAsync(Guid policyId)
	{
		var policy = await repository.GetAsync(policyId);
		policy.Deactivate();
		await repository.UpdateAsync(policy, autoSave: true);
		await ClearPolicyCachesAsync(policy.AcademicYearId);
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

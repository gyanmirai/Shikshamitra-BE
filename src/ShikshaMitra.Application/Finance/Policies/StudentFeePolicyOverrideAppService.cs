using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Domain.Finance.Policies;

[RemoteService(false)]
[Authorize]
public class StudentFeePolicyOverrideAppService(
	IRepository<StudentFeePolicyOverride, Guid> repository,
	CollectionPolicyResolver resolver)
	: ApplicationService, IStudentFeePolicyOverrideAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.View)]
	public async Task<EffectiveCollectionRulesDto> GetEffectiveRulesAsync(Guid studentId, Guid feeStructureId)
	{
		var effective = await resolver.ResolveEffectiveRulesForStudentAsync(studentId, feeStructureId);
		return new EffectiveCollectionRulesDto
		{
			Policy = effective.Policy is null ? null : CollectionPolicyMapping.ToDto(effective.Policy),
			IsLateFeeExempt = effective.IsLateFeeExempt,
			CustomGracePeriodDays = effective.CustomGracePeriodDays,
			WaiverPreApproved = effective.WaiverPreApproved
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.View)]
	public async Task<StudentFeePolicyOverrideDto?> GetOverrideAsync(Guid studentId, Guid feeStructureId)
	{
		var entity = await FindEntityAsync(studentId, feeStructureId);
		return entity is null ? null : ToDto(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.EditStudentOverride)]
	public async Task<StudentFeePolicyOverrideDto> SetOverrideAsync(Guid studentId, Guid feeStructureId, CreateUpdateStudentFeePolicyOverrideDto input)
	{
		if (input.WaiverPreApproved && string.IsNullOrWhiteSpace(input.WaiverPreApprovalReason))
		{
			throw new BusinessException(CollectionPolicyErrorCodes.WaiverPreApprovalReasonRequired);
		}

		var entity = await FindEntityAsync(studentId, feeStructureId);
		if (entity is null)
		{
			entity = new StudentFeePolicyOverride(GuidGenerator.Create(), CurrentTenant.Id, studentId, feeStructureId);
			entity.Update(input.IsLateFeeExempt, input.CustomGracePeriodDays, input.WaiverPreApproved, input.WaiverPreApprovalReason);
			await repository.InsertAsync(entity, autoSave: true);
		}
		else
		{
			entity.Update(input.IsLateFeeExempt, input.CustomGracePeriodDays, input.WaiverPreApproved, input.WaiverPreApprovalReason);
			await repository.UpdateAsync(entity, autoSave: true);
		}

		return ToDto(entity);
	}

	private async Task<StudentFeePolicyOverride?> FindEntityAsync(Guid studentId, Guid feeStructureId)
	{
		var query = await repository.GetQueryableAsync();
		return query.FirstOrDefault(x => x.StudentId == studentId && x.FeeStructureId == feeStructureId);
	}

	private static StudentFeePolicyOverrideDto ToDto(StudentFeePolicyOverride entity) => new()
	{
		Id = entity.Id,
		StudentId = entity.StudentId,
		FeeStructureId = entity.FeeStructureId,
		IsLateFeeExempt = entity.IsLateFeeExempt,
		CustomGracePeriodDays = entity.CustomGracePeriodDays,
		WaiverPreApproved = entity.WaiverPreApproved,
		WaiverPreApprovalReason = entity.WaiverPreApprovalReason,
		LastModificationTime = entity.LastModificationTime,
		LastModifierId = entity.LastModifierId
	};
}

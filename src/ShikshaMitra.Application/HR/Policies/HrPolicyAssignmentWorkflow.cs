using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;
using Volo.Abp;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
public class HrPolicyAssignmentWorkflow(
	IRepository<HrPolicyAssignment, Guid> repository,
	HrPolicyAssignmentAuditWriter auditWriter,
	HrPolicyAssignmentImpactService assignmentImpactService,
	HrPolicyEmployeeOverrideWorkflow employeeOverrideWorkflow)
	: ApplicationService, ITransientDependency
{
	public async Task<HrPolicyAssignmentDto> CreateAsync(CreateUpdateHrPolicyAssignmentDto input)
	{
		var targetType = HrPolicyMapping.ParseAssignmentTargetType(input.TargetType);
		var targetKey = HrPolicyAssignment.NormalizeTargetKey(targetType, input.TargetKey);
		await ValidateAssignmentAsync(input, targetType, targetKey, existingAssignmentId: null);
		await assignmentImpactService.EnsureAssignmentImpactConfirmedAsync("create", null, input);

		var entity = new HrPolicyAssignment(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			input.AcademicYearId,
			targetType,
			targetKey,
			input.TargetDisplayName,
			input.AttendancePolicyId,
			input.PayrollPolicyId,
			input.LeavePolicyId,
			input.EffectiveFrom,
			input.EffectiveTo,
			input.IsActive,
			input.Priority,
			input.AssignmentReason);

		await repository.InsertAsync(entity, autoSave: true);
		await auditWriter.WriteCreatedAsync(entity);
		return HrPolicyMapping.ToDto(entity);
	}

	public async Task<HrPolicyAssignmentDto> UpdateAsync(Guid id, CreateUpdateHrPolicyAssignmentDto input)
	{
		var entity = await repository.GetAsync(id);
		EnsureAcademicYearUnchanged(entity.AcademicYearId, input.AcademicYearId);
		var targetType = HrPolicyMapping.ParseAssignmentTargetType(input.TargetType);
		var targetKey = HrPolicyAssignment.NormalizeTargetKey(targetType, input.TargetKey);
		await ValidateAssignmentAsync(input, targetType, targetKey, id);
		await assignmentImpactService.EnsureAssignmentImpactConfirmedAsync("update", id, input);

		var before = HrPolicyMapping.ToDto(entity);
		entity.UpdateTarget(targetType, targetKey, input.TargetDisplayName);
		entity.Update(input.AttendancePolicyId, input.PayrollPolicyId, input.LeavePolicyId, input.EffectiveFrom, input.EffectiveTo, input.IsActive, input.Priority, input.AssignmentReason);
		await repository.UpdateAsync(entity, autoSave: true);
		var dto = HrPolicyMapping.ToDto(entity);
		await auditWriter.WriteUpdatedAsync(entity, before, dto);
		return dto;
	}

	public async Task<HrPolicyAssignmentDto> UpsertEmployeeOverrideAsync(UpsertEmployeePolicyOverrideDto input)
		=> await employeeOverrideWorkflow.UpsertAsync(input);

	public async Task DisableEmployeeOverrideAsync(DisableEmployeePolicyOverrideDto input)
		=> await employeeOverrideWorkflow.DisableAsync(input);

	public Task<HrPolicyImpactPreviewDto> PreviewImpactAsync(HrPolicyAssignmentImpactPreviewRequestDto input) =>
		assignmentImpactService.PreviewImpactAsync(input);

	public async Task DeleteAsync(Guid id)
	{
		var entity = await repository.GetAsync(id);
		if (!entity.IsActive)
		{
			return;
		}

		await assignmentImpactService.EnsureAssignmentImpactConfirmedAsync("delete", id, new CreateUpdateHrPolicyAssignmentDto
		{
			AcademicYearId = entity.AcademicYearId,
			TargetType = HrPolicyMapping.MapAssignmentTargetType(entity.TargetType),
			TargetKey = entity.TargetKey,
			TargetDisplayName = entity.TargetDisplayName,
			AttendancePolicyId = entity.AttendancePolicyId,
			PayrollPolicyId = entity.PayrollPolicyId,
			LeavePolicyId = entity.LeavePolicyId,
			EffectiveFrom = entity.EffectiveFrom,
			EffectiveTo = entity.EffectiveTo,
			IsActive = false,
			Priority = entity.Priority,
			AssignmentReason = entity.AssignmentReason
		});
		var before = HrPolicyMapping.ToDto(entity);
		entity.Update(entity.AttendancePolicyId, entity.PayrollPolicyId, entity.LeavePolicyId, entity.EffectiveFrom, entity.EffectiveTo, false, entity.Priority, entity.AssignmentReason);
		await repository.UpdateAsync(entity, autoSave: true);
		await auditWriter.WriteDeletedAsync(entity, before);
	}

	private async Task ValidateAssignmentAsync(
		CreateUpdateHrPolicyAssignmentDto input,
		HrPolicyAssignmentTargetType targetType,
		string targetKey,
		Guid? existingAssignmentId)
	{
		await assignmentImpactService.ValidateReferencesAsync(input);
		await assignmentImpactService.ValidateTargetAsync(targetType, targetKey, input.AcademicYearId);
		await assignmentImpactService.EnsureNoOverlapAsync(
			targetType,
			targetKey,
			input.AcademicYearId,
			input.EffectiveFrom,
			input.EffectiveTo,
			existingAssignmentId,
			input.IsActive);
	}

	private static void EnsureAcademicYearUnchanged(Guid entityAcademicYearId, Guid inputAcademicYearId)
	{
		if (entityAcademicYearId != inputAcademicYearId)
		{
			throw new BusinessException(HRErrorCodes.HrPolicyAssignmentAcademicYearMismatch);
		}
	}

}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
public class HrPolicyEmployeeOverrideWorkflow(
	IRepository<HrPolicyAssignment, Guid> repository,
	IRepository<Employee, Guid> employeeRepository,
	HrPolicyAssignmentAuditWriter auditWriter,
	HrPolicyAssignmentImpactService assignmentImpactService,
	HrPolicyEmployeeOverrideDisabler employeeOverrideDisabler)
	: ApplicationService, ITransientDependency
{
	public async Task<HrPolicyAssignmentDto> UpsertAsync(UpsertEmployeePolicyOverrideDto input)
	{
		var employee = await employeeRepository.GetAsync(input.EmployeeId);
		var targetType = HrPolicyAssignmentTargetType.Employee;
		var targetKey = HrPolicyAssignment.NormalizeTargetKey(targetType, input.EmployeeId.ToString("D"));
		var effectiveFrom = input.EffectiveFrom.Date;
		var effectiveTo = input.EffectiveTo?.Date;
		var payload = HrPolicyEmployeeOverridePayloadFactory.Build(input, employee, targetKey, effectiveFrom, effectiveTo);
		await assignmentImpactService.ValidateReferencesAsync(payload);
		var existing = await FindExistingAsync(input, targetType, targetKey, effectiveFrom, effectiveTo);
		if (existing == null)
		{
			await assignmentImpactService.ValidateTargetAsync(targetType, targetKey, payload.AcademicYearId);
			await assignmentImpactService.EnsureNoOverlapAsync(
				targetType,
				targetKey,
				payload.AcademicYearId,
				payload.EffectiveFrom,
				payload.EffectiveTo,
				null,
				payload.IsActive);
			await assignmentImpactService.EnsureAssignmentImpactConfirmedAsync("create", null, payload);
			return await CreateAsync(payload, targetType, targetKey);
		}

		var competing = await FindCompetingAsync(input, targetType, targetKey, effectiveFrom, effectiveTo, existing.Id);
		await assignmentImpactService.EnsureAssignmentImpactConfirmedAsync(
			HrPolicyAssignmentImpactService.EmployeeOverrideUpsertAction,
			existing.Id,
			payload);
		await DeactivateCompetingAsync(competing);
		await assignmentImpactService.EnsureNoOverlapAsync(
			targetType,
			targetKey,
			payload.AcademicYearId,
			payload.EffectiveFrom,
			payload.EffectiveTo,
			existing.Id,
			true);

		var before = HrPolicyMapping.ToDto(existing);
		existing.Update(
			payload.AttendancePolicyId,
			payload.PayrollPolicyId,
			payload.LeavePolicyId,
			payload.EffectiveFrom,
			payload.EffectiveTo,
			true,
			payload.Priority,
			payload.AssignmentReason);
		existing.UpdateTarget(targetType, targetKey, employee.EmployeeName);
		await repository.UpdateAsync(existing, autoSave: true);
		var dto = HrPolicyMapping.ToDto(existing);
		await auditWriter.WriteEmployeeOverrideAsync(existing, before, dto, employee.EmployeeName);
		return dto;
	}

	public async Task DisableAsync(DisableEmployeePolicyOverrideDto input) =>
		await employeeOverrideDisabler.DisableAsync(input);


	private async Task<HrPolicyAssignmentDto> CreateAsync(
		CreateUpdateHrPolicyAssignmentDto input,
		HrPolicyAssignmentTargetType targetType,
		string targetKey)
	{
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

	private async Task<HrPolicyAssignment?> FindExistingAsync(
		UpsertEmployeePolicyOverrideDto input,
		HrPolicyAssignmentTargetType targetType,
		string targetKey,
		DateTime effectiveFrom,
		DateTime? effectiveTo)
	{
		var query = await repository.GetQueryableAsync();
		var effectiveEnd = effectiveTo ?? DateTime.MaxValue;

		if (input.AssignmentId.HasValue)
		{
			var selected = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x =>
				x.Id == input.AssignmentId.Value &&
				x.AcademicYearId == input.AcademicYearId &&
				x.TargetType == targetType &&
				x.TargetKey == targetKey &&
				x.IsActive));
			return selected ?? throw new BusinessException(HRErrorCodes.HrPolicyImpactConfirmationInvalid);
		}

		return await AsyncExecuter.FirstOrDefaultAsync(query
			.Where(x =>
				x.AcademicYearId == input.AcademicYearId &&
				x.TargetType == targetType &&
				x.TargetKey == targetKey &&
				x.IsActive &&
				x.EffectiveFrom <= effectiveEnd &&
				(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= effectiveFrom))
			.OrderByDescending(x => x.Priority)
			.ThenByDescending(x => x.EffectiveFrom)
			.ThenBy(x => x.Id));
	}

	private async Task<List<HrPolicyAssignment>> FindCompetingAsync(
		UpsertEmployeePolicyOverrideDto input,
		HrPolicyAssignmentTargetType targetType,
		string targetKey,
		DateTime effectiveFrom,
		DateTime? effectiveTo,
		Guid keepAssignmentId)
	{
		var query = await repository.GetQueryableAsync();
		var effectiveEnd = effectiveTo ?? DateTime.MaxValue.Date;
		return await AsyncExecuter.ToListAsync(query.Where(x =>
			x.AcademicYearId == input.AcademicYearId &&
			x.TargetType == targetType &&
			x.TargetKey == targetKey &&
			x.IsActive &&
			x.Id != keepAssignmentId &&
			x.EffectiveFrom <= effectiveEnd &&
			(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= effectiveFrom)));
	}

	private async Task DeactivateCompetingAsync(List<HrPolicyAssignment> competing)
	{
		foreach (var assignment in competing)
		{
			var before = HrPolicyMapping.ToDto(assignment);
			assignment.Update(
				assignment.AttendancePolicyId,
				assignment.PayrollPolicyId,
				assignment.LeavePolicyId,
				assignment.EffectiveFrom,
				assignment.EffectiveTo,
				false,
				assignment.Priority,
				assignment.AssignmentReason);
			await repository.UpdateAsync(assignment, autoSave: false);
			await auditWriter.WriteDeletedAsync(assignment, before);
		}

		if (competing.Count > 0)
		{
			await CurrentUnitOfWork!.SaveChangesAsync();
		}
	}

}

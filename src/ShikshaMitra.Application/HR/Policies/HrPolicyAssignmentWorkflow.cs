using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;
using Volo.Abp;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
public class HrPolicyAssignmentWorkflow(
	IRepository<HrPolicyAssignment, Guid> repository,
	IRepository<Employee, Guid> employeeRepository,
	HrPolicyAssignmentAuditWriter auditWriter,
	HrPolicyAssignmentImpactService assignmentImpactService)
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
		var targetType = HrPolicyMapping.ParseAssignmentTargetType(input.TargetType);
		var targetKey = HrPolicyAssignment.NormalizeTargetKey(targetType, input.TargetKey);
		await ValidateAssignmentAsync(input, targetType, targetKey, id);
		await assignmentImpactService.EnsureAssignmentImpactConfirmedAsync("update", id, input);

		var entity = await repository.GetAsync(id);
		var before = HrPolicyMapping.ToDto(entity);
		entity.UpdateTarget(targetType, targetKey, input.TargetDisplayName);
		entity.Update(input.AttendancePolicyId, input.PayrollPolicyId, input.EffectiveFrom, input.EffectiveTo, input.IsActive, input.Priority, input.AssignmentReason);
		await repository.UpdateAsync(entity, autoSave: true);
		var dto = HrPolicyMapping.ToDto(entity);
		await auditWriter.WriteUpdatedAsync(entity, before, dto);
		return dto;
	}

	public async Task<HrPolicyAssignmentDto> UpsertEmployeeOverrideAsync(UpsertEmployeePolicyOverrideDto input)
	{
		var employee = await employeeRepository.GetAsync(input.EmployeeId);
		var targetType = HrPolicyAssignmentTargetType.Employee;
		var targetKey = HrPolicyAssignment.NormalizeTargetKey(targetType, input.EmployeeId.ToString("D"));
		var effectiveFrom = input.EffectiveFrom.Date;
		var effectiveTo = input.EffectiveTo?.Date;
		var payload = BuildEmployeeOverridePayload(input, employee, targetKey, effectiveFrom, effectiveTo);

		await assignmentImpactService.ValidateReferencesAsync(payload);
		var existing = await FindExistingEmployeeOverrideAsync(input, targetType, targetKey, effectiveFrom, effectiveTo);
		if (existing == null)
		{
			return await CreateAsync(payload);
		}

		await DeactivateCompetingEmployeeOverridesAsync(input, targetType, targetKey, effectiveFrom, effectiveTo, existing.Id);
		await assignmentImpactService.EnsureNoOverlapAsync(targetType, targetKey, payload.AcademicYearId, payload.EffectiveFrom, payload.EffectiveTo, existing.Id, true);
		await assignmentImpactService.EnsureAssignmentImpactConfirmedAsync("update", existing.Id, payload);

		var before = HrPolicyMapping.ToDto(existing);
		existing.Update(payload.AttendancePolicyId, payload.PayrollPolicyId, payload.EffectiveFrom, payload.EffectiveTo, true, payload.Priority, payload.AssignmentReason);
		existing.UpdateTarget(targetType, targetKey, employee.EmployeeName);
		await repository.UpdateAsync(existing, autoSave: true);
		var dto = HrPolicyMapping.ToDto(existing);
		await auditWriter.WriteEmployeeOverrideAsync(existing, before, dto, employee.EmployeeName);
		return dto;
	}

	public async Task DisableEmployeeOverrideAsync(DisableEmployeePolicyOverrideDto input)
	{
		if (input.EffectiveDate == default)
		{
			throw new BusinessException(HRErrorCodes.PolicyAssignmentRangeInvalid);
		}

		await employeeRepository.GetAsync(input.EmployeeId);
		var targetType = HrPolicyAssignmentTargetType.Employee;
		var targetKey = HrPolicyAssignment.NormalizeTargetKey(targetType, input.EmployeeId.ToString("D"));
		var effectiveDate = input.EffectiveDate.Date;
		var query = await repository.GetQueryableAsync();
		var activeOverrides = await AsyncExecuter.ToListAsync(query.Where(x =>
			x.AcademicYearId == input.AcademicYearId &&
			x.TargetType == targetType &&
			x.TargetKey == targetKey &&
			x.IsActive &&
			x.EffectiveFrom <= effectiveDate &&
			(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= effectiveDate)));

		foreach (var assignment in activeOverrides)
		{
			var before = HrPolicyMapping.ToDto(assignment);
			var disableReason = string.IsNullOrWhiteSpace(input.AssignmentReason)
				? assignment.AssignmentReason
				: input.AssignmentReason;
			var shouldEndBeforeEffectiveDate = assignment.EffectiveFrom.Date < effectiveDate;
			assignment.Update(
				assignment.AttendancePolicyId,
				assignment.PayrollPolicyId,
				assignment.EffectiveFrom,
				shouldEndBeforeEffectiveDate ? effectiveDate.AddDays(-1) : assignment.EffectiveTo,
				shouldEndBeforeEffectiveDate,
				assignment.Priority,
				disableReason);
			await repository.UpdateAsync(assignment, autoSave: false);
			await auditWriter.WriteDeletedAsync(assignment, before);
		}

		if (activeOverrides.Count > 0)
		{
			await CurrentUnitOfWork!.SaveChangesAsync();
		}
	}

	public Task<HrPolicyImpactPreviewDto> PreviewImpactAsync(HrPolicyAssignmentImpactPreviewRequestDto input) =>
		assignmentImpactService.PreviewImpactAsync(input);

	public async Task DeleteAsync(Guid id)
	{
		var entity = await repository.GetAsync(id);
		await assignmentImpactService.EnsureAssignmentImpactConfirmedAsync("delete", id, new CreateUpdateHrPolicyAssignmentDto
		{
			AcademicYearId = entity.AcademicYearId,
			TargetType = HrPolicyMapping.MapAssignmentTargetType(entity.TargetType),
			TargetKey = entity.TargetKey,
			TargetDisplayName = entity.TargetDisplayName,
			AttendancePolicyId = entity.AttendancePolicyId,
			PayrollPolicyId = entity.PayrollPolicyId,
			EffectiveFrom = entity.EffectiveFrom,
			EffectiveTo = entity.EffectiveTo,
			IsActive = false,
			Priority = entity.Priority,
			AssignmentReason = entity.AssignmentReason
		});
		var before = HrPolicyMapping.ToDto(entity);
		entity.Update(entity.AttendancePolicyId, entity.PayrollPolicyId, entity.EffectiveFrom, entity.EffectiveTo, false, entity.Priority, entity.AssignmentReason);
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
		await assignmentImpactService.ValidateTargetAsync(targetType, targetKey);
		await assignmentImpactService.EnsureNoOverlapAsync(
			targetType,
			targetKey,
			input.AcademicYearId,
			input.EffectiveFrom,
			input.EffectiveTo,
			existingAssignmentId,
			input.IsActive);
	}

	private async Task<HrPolicyAssignment?> FindExistingEmployeeOverrideAsync(
		UpsertEmployeePolicyOverrideDto input,
		HrPolicyAssignmentTargetType targetType,
		string targetKey,
		DateTime effectiveFrom,
		DateTime? effectiveTo)
	{
		var query = await repository.GetQueryableAsync();
		var effectiveEnd = effectiveTo ?? DateTime.MaxValue;
		return await AsyncExecuter.FirstOrDefaultAsync(query
			.Where(x =>
				x.AcademicYearId == input.AcademicYearId &&
				x.TargetType == targetType &&
				x.TargetKey == targetKey &&
				x.IsActive &&
				x.EffectiveFrom <= effectiveEnd &&
				(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= effectiveFrom))
			.OrderByDescending(x => x.Priority)
			.ThenByDescending(x => x.EffectiveFrom));
	}

	private async Task DeactivateCompetingEmployeeOverridesAsync(
		UpsertEmployeePolicyOverrideDto input,
		HrPolicyAssignmentTargetType targetType,
		string targetKey,
		DateTime effectiveFrom,
		DateTime? effectiveTo,
		Guid keepAssignmentId)
	{
		var query = await repository.GetQueryableAsync();
		var effectiveEnd = effectiveTo ?? DateTime.MaxValue.Date;
		var competing = await AsyncExecuter.ToListAsync(query.Where(x =>
			x.AcademicYearId == input.AcademicYearId &&
			x.TargetType == targetType &&
			x.TargetKey == targetKey &&
			x.IsActive &&
			x.Id != keepAssignmentId &&
			x.EffectiveFrom <= effectiveEnd &&
			(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= effectiveFrom)));

		foreach (var assignment in competing)
		{
			var before = HrPolicyMapping.ToDto(assignment);
			assignment.Update(
				assignment.AttendancePolicyId,
				assignment.PayrollPolicyId,
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

	private static CreateUpdateHrPolicyAssignmentDto BuildEmployeeOverridePayload(
		UpsertEmployeePolicyOverrideDto input,
		Employee employee,
		string targetKey,
		DateTime effectiveFrom,
		DateTime? effectiveTo) => new()
	{
		AcademicYearId = input.AcademicYearId,
		TargetType = HrPolicyMapping.MapAssignmentTargetType(HrPolicyAssignmentTargetType.Employee),
		TargetKey = targetKey,
		TargetDisplayName = employee.EmployeeName,
		AttendancePolicyId = input.AttendancePolicyId,
		PayrollPolicyId = input.PayrollPolicyId,
		EffectiveFrom = effectiveFrom,
		EffectiveTo = effectiveTo,
		IsActive = true,
		Priority = 100,
		AssignmentReason = string.IsNullOrWhiteSpace(input.AssignmentReason)
			? "Employee payroll step override"
			: input.AssignmentReason,
		ConfirmationToken = input.ConfirmationToken
	};

}

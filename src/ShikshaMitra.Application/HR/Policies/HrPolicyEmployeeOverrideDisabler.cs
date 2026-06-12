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
public class HrPolicyEmployeeOverrideDisabler(
	IRepository<HrPolicyAssignment, Guid> repository,
	IRepository<Employee, Guid> employeeRepository,
	HrPolicyAssignmentAuditWriter auditWriter,
	HrPolicyAssignmentImpactService assignmentImpactService)
	: ApplicationService, ITransientDependency
{
	public async Task DisableAsync(DisableEmployeePolicyOverrideDto input)
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
		if (activeOverrides.Count == 0)
		{
			return;
		}

		var confirmationAssignment = ResolveConfirmationAssignment(input, activeOverrides);
		var impactPayload = BuildDisableImpactPayload(input, confirmationAssignment, targetKey);
		await assignmentImpactService.EnsureAssignmentImpactConfirmedAsync(
			HrPolicyAssignmentImpactService.EmployeeOverrideDisableAction,
			confirmationAssignment.Id,
			impactPayload);

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
				assignment.LeavePolicyId,
				assignment.EffectiveFrom,
				shouldEndBeforeEffectiveDate ? effectiveDate.AddDays(-1) : assignment.EffectiveTo,
				shouldEndBeforeEffectiveDate,
				assignment.Priority,
				disableReason);
			await repository.UpdateAsync(assignment, autoSave: false);
			await auditWriter.WriteDeletedAsync(assignment, before);
		}

		await CurrentUnitOfWork!.SaveChangesAsync();
	}

	private static CreateUpdateHrPolicyAssignmentDto BuildDisableImpactPayload(
		DisableEmployeePolicyOverrideDto input,
		HrPolicyAssignment assignment,
		string targetKey) =>
		new()
		{
			AcademicYearId = input.AcademicYearId,
			TargetType = "employee",
			TargetKey = targetKey,
			TargetDisplayName = assignment.TargetDisplayName,
			AttendancePolicyId = assignment.AttendancePolicyId,
			PayrollPolicyId = assignment.PayrollPolicyId,
			LeavePolicyId = assignment.LeavePolicyId ?? Guid.Empty,
			EffectiveFrom = input.EffectiveDate.Date,
			EffectiveTo = assignment.EffectiveTo,
			IsActive = false,
			Priority = assignment.Priority,
			AssignmentReason = input.AssignmentReason,
			ConfirmationToken = input.ConfirmationToken
		};

	private static HrPolicyAssignment ResolveConfirmationAssignment(
		DisableEmployeePolicyOverrideDto input,
		IReadOnlyCollection<HrPolicyAssignment> activeOverrides)
	{
		if (input.AssignmentId.HasValue)
		{
			return activeOverrides.FirstOrDefault(x => x.Id == input.AssignmentId.Value)
				?? throw new BusinessException(HRErrorCodes.HrPolicyImpactConfirmationInvalid);
		}

		return activeOverrides
			.OrderByDescending(x => x.Priority)
			.ThenByDescending(x => x.EffectiveFrom)
			.ThenBy(x => x.Id)
			.First();
	}
}

using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
public class HrPolicyArchiveAssignmentRepairService(
	IRepository<HrPolicyAssignment, Guid> assignmentRepository,
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	HrPolicyWorkCalendarValidator workCalendarValidator,
	HrPolicyAssignmentAuditWriter auditWriter) : ApplicationService, ITransientDependency
{
	private const int AssignmentReasonMaxLength = 256;

	public async Task ReassignPayrollReferencesToDefaultAsync(PayrollPolicy policy)
	{
		var query = await assignmentRepository.GetQueryableAsync();
		var assignments = await AsyncExecuter.ToListAsync(query.Where(x =>
			x.AcademicYearId == policy.AcademicYearId &&
			x.IsActive &&
			x.PayrollPolicyId == policy.Id));
		if (assignments.Count == 0)
		{
			return;
		}

		var defaultPolicy = await ResolveDefaultPayrollPolicyAsync(policy);
		await workCalendarValidator.EnsureUsableAsync(defaultPolicy.WorkCalendarPolicyId, policy.AcademicYearId, defaultPolicy.Status);

		foreach (var assignment in assignments)
		{
			var before = HrPolicyMapping.ToDto(assignment);
			var reason = BuildArchiveRepairReason(assignment.AssignmentReason, "payroll", policy.Name, defaultPolicy.Name);
			assignment.Update(
				assignment.AttendancePolicyId,
				defaultPolicy.Id,
				assignment.LeavePolicyId,
				assignment.EffectiveFrom,
				assignment.EffectiveTo,
				assignment.IsActive,
				assignment.Priority,
				reason);
			await assignmentRepository.UpdateAsync(assignment, autoSave: false);
			await auditWriter.WriteUpdatedAsync(assignment, before, HrPolicyMapping.ToDto(assignment));
		}

		await CurrentUnitOfWork!.SaveChangesAsync();
	}

	public async Task ReassignAttendanceReferencesToDefaultAsync(AttendancePolicy policy)
	{
		var query = await assignmentRepository.GetQueryableAsync();
		var assignments = await AsyncExecuter.ToListAsync(query.Where(x =>
			x.AcademicYearId == policy.AcademicYearId &&
			x.IsActive &&
			x.AttendancePolicyId == policy.Id));
		if (assignments.Count == 0)
		{
			return;
		}

		var defaultPolicy = await ResolveDefaultAttendancePolicyAsync(policy);
		await workCalendarValidator.EnsureUsableAsync(defaultPolicy.WorkCalendarPolicyId, policy.AcademicYearId, defaultPolicy.Status);

		foreach (var assignment in assignments)
		{
			var before = HrPolicyMapping.ToDto(assignment);
			var reason = BuildArchiveRepairReason(assignment.AssignmentReason, "attendance", policy.Name, defaultPolicy.Name);
			assignment.Update(
				defaultPolicy.Id,
				assignment.PayrollPolicyId,
				assignment.LeavePolicyId,
				assignment.EffectiveFrom,
				assignment.EffectiveTo,
				assignment.IsActive,
				assignment.Priority,
				reason);
			await assignmentRepository.UpdateAsync(assignment, autoSave: false);
			await auditWriter.WriteUpdatedAsync(assignment, before, HrPolicyMapping.ToDto(assignment));
		}

		await CurrentUnitOfWork!.SaveChangesAsync();
	}

	private async Task<PayrollPolicy> ResolveDefaultPayrollPolicyAsync(PayrollPolicy policy)
	{
		var query = await payrollPolicyRepository.GetQueryableAsync();
		var defaults = await AsyncExecuter.ToListAsync(query.Where(x =>
			x.AcademicYearId == policy.AcademicYearId &&
			x.Id != policy.Id &&
			x.IsDefault &&
			x.Status == HrPolicyStatus.Active));

		if (defaults.Count != 1)
		{
			throw new BusinessException(HRErrorCodes.DefaultPayrollPolicyRequired)
				.WithData("defaultCount", defaults.Count);
		}

		return defaults[0];
	}

	private async Task<AttendancePolicy> ResolveDefaultAttendancePolicyAsync(AttendancePolicy policy)
	{
		var query = await attendancePolicyRepository.GetQueryableAsync();
		var defaults = await AsyncExecuter.ToListAsync(query.Where(x =>
			x.AcademicYearId == policy.AcademicYearId &&
			x.Id != policy.Id &&
			x.IsDefault &&
			x.Status == HrPolicyStatus.Active));

		if (defaults.Count != 1)
		{
			throw new BusinessException(HRErrorCodes.DefaultAttendancePolicyRequired)
				.WithData("defaultCount", defaults.Count);
		}

		return defaults[0];
	}

	private static string BuildArchiveRepairReason(
		string? existingReason,
		string policyType,
		string archivedPolicyName,
		string defaultPolicyName)
	{
		var repairReason = $"Reassigned to default {policyType} policy \"{defaultPolicyName}\" because \"{archivedPolicyName}\" was archived.";
		var value = string.IsNullOrWhiteSpace(existingReason)
			? repairReason
			: $"{existingReason} {repairReason}";
		return value.Length <= AssignmentReasonMaxLength
			? value
			: value[..AssignmentReasonMaxLength];
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.Employees;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyEffectiveAssignmentLookupReader(
	IRepository<HrPolicyAssignment, Guid> assignmentRepository,
	IRepository<Employee, Guid> employeeRepository,
	IGroupRepository groupRepository,
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public async Task<HrPolicyEffectiveAssignmentLookup> ReadAsync(
		CreateUpdateHrPolicyAssignmentDto proposed,
		IReadOnlyCollection<HrPolicyAffectedEmployeeDto> affectedEmployees,
		Guid proposedGroupId,
		Guid? proposedAssignmentId)
	{
		var groups = await groupRepository.GetListWithDetailsAsync(proposed.AcademicYearId);
		var proposedGroup = groups.FirstOrDefault(x => x.Id == proposedGroupId);
		var activeAssignments = await GetActiveAssignmentsAsync(proposed, proposedAssignmentId);
		var employees = await GetEmployeesAsync(affectedEmployees);

		return new HrPolicyEffectiveAssignmentLookup(
			proposedGroup,
			employees,
			activeAssignments,
			groups.ToDictionary(x => x.Id),
			await GetPayrollNamesAsync(activeAssignments, proposed.PayrollPolicyId),
			await GetAttendanceNamesAsync(activeAssignments, proposed.AttendancePolicyId));
	}

	private async Task<List<Employee>> GetEmployeesAsync(IReadOnlyCollection<HrPolicyAffectedEmployeeDto> affectedEmployees)
	{
		var affectedEmployeeIds = affectedEmployees.Select(e => e.EmployeeId).ToList();
		var employeeQuery = await employeeRepository.GetQueryableAsync();
		return await asyncExecuter.ToListAsync(employeeQuery.Where(x => affectedEmployeeIds.Contains(x.Id)));
	}

	private async Task<List<HrPolicyAssignment>> GetActiveAssignmentsAsync(
		CreateUpdateHrPolicyAssignmentDto proposed,
		Guid? excludeId)
	{
		var start = proposed.EffectiveFrom.Date;
		var end = proposed.EffectiveTo?.Date ?? DateTime.MaxValue.Date;
		var assignmentQuery = await assignmentRepository.GetQueryableAsync();
		return await asyncExecuter.ToListAsync(assignmentQuery.Where(x =>
			x.AcademicYearId == proposed.AcademicYearId &&
			(x.TargetType == HrPolicyAssignmentTargetType.Group || x.TargetType == HrPolicyAssignmentTargetType.Employee) &&
			x.IsActive &&
			(!excludeId.HasValue || x.Id != excludeId.Value) &&
			x.EffectiveFrom <= end &&
			(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= start)));
	}

	private async Task<Dictionary<Guid, string>> GetPayrollNamesAsync(
		IReadOnlyCollection<HrPolicyAssignment> activeAssignments,
		Guid proposedPayrollPolicyId)
	{
		var payrollIds = activeAssignments.Select(x => x.PayrollPolicyId).Append(proposedPayrollPolicyId).Distinct().ToList();
		var payrollQuery = await payrollPolicyRepository.GetQueryableAsync();
		return (await asyncExecuter.ToListAsync(payrollQuery.Where(x => payrollIds.Contains(x.Id))))
			.ToDictionary(x => x.Id, x => x.Name);
	}

	private async Task<Dictionary<Guid, string>> GetAttendanceNamesAsync(
		IReadOnlyCollection<HrPolicyAssignment> activeAssignments,
		Guid proposedAttendancePolicyId)
	{
		var attendanceIds = activeAssignments.Select(x => x.AttendancePolicyId).Append(proposedAttendancePolicyId).Distinct().ToList();
		var attendanceQuery = await attendancePolicyRepository.GetQueryableAsync();
		return (await asyncExecuter.ToListAsync(attendanceQuery.Where(x => attendanceIds.Contains(x.Id))))
			.ToDictionary(x => x.Id, x => x.Name);
	}
}

public sealed record HrPolicyEffectiveAssignmentLookup(
	Group? ProposedGroup,
	IReadOnlyCollection<Employee> Employees,
	IReadOnlyCollection<HrPolicyAssignment> ActiveAssignments,
	IReadOnlyDictionary<Guid, Group> GroupMap,
	IReadOnlyDictionary<Guid, string> PayrollNames,
	IReadOnlyDictionary<Guid, string> AttendanceNames);

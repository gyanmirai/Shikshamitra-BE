using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.Employees;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using static ShikshaMitra.HR.Policies.HrPolicyAssignmentProjection;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyEffectiveAssignmentConflictBuilder(
	IRepository<HrPolicyAssignment, Guid> assignmentRepository,
	IRepository<Employee, Guid> employeeRepository,
	IGroupRepository groupRepository,
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task<List<HrPolicyEffectiveAssignmentConflictDto>> BuildAsync(
		HrPolicyAssignmentTargetType targetType,
		string targetKey,
		CreateUpdateHrPolicyAssignmentDto proposed,
		IReadOnlyCollection<HrPolicyAffectedEmployeeDto> affectedEmployees)
	{
		if (targetType != HrPolicyAssignmentTargetType.Group || affectedEmployees.Count == 0 || !Guid.TryParse(targetKey, out var proposedGroupId))
		{
			return [];
		}

		var date = proposed.EffectiveFrom.Date;
		var groups = await groupRepository.GetListWithDetailsAsync(proposed.AcademicYearId);
		var proposedGroup = groups.FirstOrDefault(x => x.Id == proposedGroupId);
		if (proposedGroup == null)
		{
			return [];
		}

		var affectedEmployeeIds = affectedEmployees.Select(e => e.EmployeeId).ToList();
		var employeeQuery = await employeeRepository.GetQueryableAsync();
		var employees = await asyncExecuter.ToListAsync(employeeQuery.Where(x => affectedEmployeeIds.Contains(x.Id)));
		var activeGroupAssignments = await GetActiveGroupAssignmentsAsync(proposed.AcademicYearId, date);
		var payrollNames = await GetPayrollNamesAsync(activeGroupAssignments, proposed.PayrollPolicyId);
		var attendanceNames = await GetAttendanceNamesAsync(activeGroupAssignments, proposed.AttendancePolicyId);
		var groupMap = groups.ToDictionary(x => x.Id);

		var conflicts = new List<HrPolicyEffectiveAssignmentConflictDto>();
		foreach (var employee in employees)
		{
			var candidates = BuildCandidates(
				employee,
				proposedGroup,
				targetKey,
				proposed,
				activeGroupAssignments,
				groupMap,
				payrollNames,
				attendanceNames);

			if (candidates.Count <= 1)
			{
				continue;
			}

			var winner = MarkWinner(candidates);
			conflicts.Add(new HrPolicyEffectiveAssignmentConflictDto
			{
				EmployeeId = employee.Id,
				EmployeeCode = employee.EmployeeCode,
				EmployeeName = employee.EmployeeName,
				Date = date,
				WinningAssignment = winner,
				CandidateAssignments = candidates
			});
		}

		return conflicts;
	}

	private async Task<List<HrPolicyAssignment>> GetActiveGroupAssignmentsAsync(Guid academicYearId, DateTime date)
	{
		var assignmentQuery = await assignmentRepository.GetQueryableAsync();
		return await asyncExecuter.ToListAsync(assignmentQuery.Where(x =>
			x.AcademicYearId == academicYearId &&
			x.TargetType == HrPolicyAssignmentTargetType.Group &&
			x.IsActive &&
			x.EffectiveFrom <= date &&
			(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= date)));
	}

	private async Task<Dictionary<Guid, string>> GetPayrollNamesAsync(
		IReadOnlyCollection<HrPolicyAssignment> activeGroupAssignments,
		Guid proposedPayrollPolicyId)
	{
		var payrollIds = activeGroupAssignments.Select(x => x.PayrollPolicyId).Append(proposedPayrollPolicyId).Distinct().ToList();
		var payrollQuery = await payrollPolicyRepository.GetQueryableAsync();
		return (await asyncExecuter.ToListAsync(payrollQuery.Where(x => payrollIds.Contains(x.Id))))
			.ToDictionary(x => x.Id, x => x.Name);
	}

	private async Task<Dictionary<Guid, string>> GetAttendanceNamesAsync(
		IReadOnlyCollection<HrPolicyAssignment> activeGroupAssignments,
		Guid proposedAttendancePolicyId)
	{
		var attendanceIds = activeGroupAssignments.Select(x => x.AttendancePolicyId).Append(proposedAttendancePolicyId).Distinct().ToList();
		var attendanceQuery = await attendancePolicyRepository.GetQueryableAsync();
		return (await asyncExecuter.ToListAsync(attendanceQuery.Where(x => attendanceIds.Contains(x.Id))))
			.ToDictionary(x => x.Id, x => x.Name);
	}

	private static List<HrPolicyEffectiveAssignmentCandidateDto> BuildCandidates(
		Employee employee,
		Group proposedGroup,
		string targetKey,
		CreateUpdateHrPolicyAssignmentDto proposed,
		IReadOnlyCollection<HrPolicyAssignment> activeGroupAssignments,
		IReadOnlyDictionary<Guid, Group> groupMap,
		IReadOnlyDictionary<Guid, string> payrollNames,
		IReadOnlyDictionary<Guid, string> attendanceNames)
	{
		var candidates = new List<HrPolicyEffectiveAssignmentCandidateDto>();
		if (HrPolicyGroupMembership.MatchesEmployee(proposedGroup, employee))
		{
			candidates.Add(MapCandidate(
				null,
				targetKey,
				proposed.TargetDisplayName,
				proposed.PayrollPolicyId,
				proposed.AttendancePolicyId,
				proposed.EffectiveFrom,
				proposed.EffectiveTo,
				proposed.Priority,
				payrollNames,
				attendanceNames));
		}

		foreach (var assignment in activeGroupAssignments)
		{
			if (!Guid.TryParse(assignment.TargetKey, out var groupId) ||
				!groupMap.TryGetValue(groupId, out var group) ||
				!HrPolicyGroupMembership.MatchesEmployee(group, employee))
			{
				continue;
			}

			candidates.Add(MapCandidate(
				assignment.Id,
				assignment.TargetKey,
				assignment.TargetDisplayName,
				assignment.PayrollPolicyId,
				assignment.AttendancePolicyId,
				assignment.EffectiveFrom,
				assignment.EffectiveTo,
				assignment.Priority,
				payrollNames,
				attendanceNames));
		}

		return candidates;
	}

	private static HrPolicyEffectiveAssignmentCandidateDto MarkWinner(List<HrPolicyEffectiveAssignmentCandidateDto> candidates)
	{
		var winner = candidates
			.OrderByDescending(x => x.Priority)
			.ThenByDescending(x => x.EffectiveFrom)
			.First();

		foreach (var candidate in candidates)
		{
			candidate.Status = ReferenceEquals(candidate, winner) ? "matched" : "skipped";
			candidate.SkipReason = ReferenceEquals(candidate, winner) ? "winner" : "lower_priority";
		}

		return winner;
	}
}

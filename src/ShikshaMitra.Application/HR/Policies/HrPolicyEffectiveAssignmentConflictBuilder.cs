using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.Employees;
using Volo.Abp.DependencyInjection;
using static ShikshaMitra.HR.Policies.HrPolicyAssignmentProjection;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyEffectiveAssignmentConflictBuilder(
	HrPolicyEffectiveAssignmentLookupReader lookupReader) : ITransientDependency
{
	public async Task<List<HrPolicyEffectiveAssignmentConflictDto>> BuildAsync(
		HrPolicyAssignmentTargetType targetType,
		string targetKey,
		CreateUpdateHrPolicyAssignmentDto proposed,
		IReadOnlyCollection<HrPolicyAffectedEmployeeDto> affectedEmployees,
		Guid? proposedAssignmentId = null)
	{
		if (targetType != HrPolicyAssignmentTargetType.Group || affectedEmployees.Count == 0 || !Guid.TryParse(targetKey, out var proposedGroupId))
		{
			return [];
		}

		var lookup = await lookupReader.ReadAsync(proposed, affectedEmployees, proposedGroupId, proposedAssignmentId);
		if (lookup.ProposedGroup == null)
		{
			return [];
		}

		var start = proposed.EffectiveFrom.Date;

		var conflicts = new List<HrPolicyEffectiveAssignmentConflictDto>();
		foreach (var employee in lookup.Employees)
		{
			var candidates = BuildCandidates(
				employee,
				lookup.ProposedGroup,
				targetKey,
				proposed,
				lookup.ActiveAssignments,
				lookup.GroupMap,
				lookup.PayrollNames,
				lookup.AttendanceNames);

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
				Date = candidates
					.Where(x => x.AssignmentId.HasValue)
					.Select(x => x.EffectiveFrom > start ? x.EffectiveFrom : start)
					.DefaultIfEmpty(start)
					.Min(),
				WinningAssignment = winner,
				CandidateAssignments = candidates
			});
		}

		return conflicts;
	}

	private static List<HrPolicyEffectiveAssignmentCandidateDto> BuildCandidates(
		Employee employee,
		Group proposedGroup,
		string targetKey,
		CreateUpdateHrPolicyAssignmentDto proposed,
		IReadOnlyCollection<HrPolicyAssignment> activeAssignments,
		IReadOnlyDictionary<Guid, Group> groupMap,
		IReadOnlyDictionary<Guid, string> payrollNames,
		IReadOnlyDictionary<Guid, string> attendanceNames)
	{
		var candidates = new List<HrPolicyEffectiveAssignmentCandidateDto>();
		if (HrPolicyGroupMembership.MatchesEmployee(proposedGroup, employee))
		{
			candidates.Add(MapCandidate(
				null,
				HrPolicyAssignmentTargetType.Group,
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

		foreach (var assignment in activeAssignments)
		{
			if (!MatchesEmployee(assignment, employee, groupMap))
			{
				continue;
			}

			candidates.Add(MapCandidate(
				assignment.Id,
				assignment.TargetType,
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

	private static bool MatchesEmployee(
		HrPolicyAssignment assignment,
		Employee employee,
		IReadOnlyDictionary<Guid, Group> groupMap) => assignment.TargetType switch
	{
		HrPolicyAssignmentTargetType.Employee =>
			string.Equals(assignment.TargetKey, employee.Id.ToString("D"), StringComparison.OrdinalIgnoreCase),
		HrPolicyAssignmentTargetType.Group when Guid.TryParse(assignment.TargetKey, out var groupId) &&
			groupMap.TryGetValue(groupId, out var group) =>
			HrPolicyGroupMembership.MatchesEmployee(group, employee),
		_ => false
	};

	private static HrPolicyEffectiveAssignmentCandidateDto MarkWinner(List<HrPolicyEffectiveAssignmentCandidateDto> candidates)
	{
		var winner = candidates
			.OrderByDescending(x => string.Equals(x.TargetType, "employee", StringComparison.OrdinalIgnoreCase) ? 100 : 50)
			.ThenByDescending(x => x.Priority)
			.ThenByDescending(x => x.EffectiveFrom)
			.ThenBy(x => x.AssignmentId ?? Guid.Empty)
			.First();

		foreach (var candidate in candidates)
		{
			candidate.Status = ReferenceEquals(candidate, winner) ? "matched" : "skipped";
			candidate.SkipReason = GetSkipReason(candidate, winner);
		}

		return winner;
	}

	private static string GetSkipReason(
		HrPolicyEffectiveAssignmentCandidateDto candidate,
		HrPolicyEffectiveAssignmentCandidateDto winner)
	{
		if (ReferenceEquals(candidate, winner))
		{
			return "winner";
		}

		var candidateSpecificity = GetSpecificity(candidate.TargetType);
		var winnerSpecificity = GetSpecificity(winner.TargetType);
		if (candidateSpecificity < winnerSpecificity)
		{
			return "lower_specificity";
		}

		if (candidate.Priority < winner.Priority)
		{
			return "lower_priority";
		}

		if (candidate.EffectiveFrom.Date < winner.EffectiveFrom.Date)
		{
			return "lower_effective_date";
		}

		return "deterministic_tie_breaker";
	}

	private static int GetSpecificity(string targetType) =>
		string.Equals(targetType, "employee", StringComparison.OrdinalIgnoreCase) ? 100 : 50;
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.Employees;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyAssignmentResolver(
	IRepository<HrPolicyAssignment, Guid> assignmentRepository,
	IGroupRepository groupRepository)
	: DomainService
{
	private Dictionary<Guid, List<Group>>? batchGroups;
	private Dictionary<(Guid, DateTime), List<HrPolicyAssignment>>? batchAssignments;
	private Dictionary<Guid, ILookup<string, HrPolicyAssignment>>? batchAssignmentsByTarget;
	private Dictionary<(Guid Year, Guid Employee), List<PolicyTargetKey>>? batchTargets;
	private (DateTime Start, DateTime End)? batchPeriod;

	internal IDisposable BeginReadBatch((DateTime Start, DateTime End)? period = null)
	{
		var previousGroups = batchGroups;
		var previousAssignments = batchAssignments;
		var previousAssignmentsByTarget = batchAssignmentsByTarget;
		var previousTargets = batchTargets;
		var previousPeriod = batchPeriod;
		batchGroups = new();
		batchAssignments = period == null ? new() : null;
		batchAssignmentsByTarget = period != null ? new() : null;
		batchTargets = period != null ? new() : null;
		batchPeriod = period;
		return new Volo.Abp.DisposeAction(() =>
		{
			batchGroups = previousGroups;
			batchAssignments = previousAssignments;
			batchAssignmentsByTarget = previousAssignmentsByTarget;
			batchTargets = previousTargets;
			batchPeriod = previousPeriod;
		});
	}
	internal async Task<HrPolicyAssignmentResolution> ResolveForEmployeeAsync(
		Employee employee,
		Guid academicYearId,
		DateTime date,
		CancellationToken cancellationToken,
		HrPolicyAssignment? proposedAssignment = null,
		Guid? replacedAssignmentId = null,
		bool includeSteps = true)
	{
		return await ResolveAsync(employee, null, academicYearId, date, cancellationToken, proposedAssignment, replacedAssignmentId, includeSteps);
	}

	internal async Task<HrPolicyAssignmentResolution> ResolvePreviewAsync(
		Guid? groupId,
		Guid academicYearId,
		DateTime date,
		CancellationToken cancellationToken,
		HrPolicyAssignment? proposedAssignment = null,
		Guid? replacedAssignmentId = null)
	{
		return await ResolveAsync(null, groupId, academicYearId, date, cancellationToken, proposedAssignment, replacedAssignmentId);
	}

	private async Task<HrPolicyAssignmentResolution> ResolveAsync(
		Employee? employee,
		Guid? groupId,
		Guid academicYearId,
		DateTime date,
		CancellationToken cancellationToken,
		HrPolicyAssignment? proposedAssignment,
		Guid? replacedAssignmentId,
		bool includeSteps = true)
	{
		var targets = await GetTargetKeysAsync(employee, groupId, academicYearId, cancellationToken);
		if (targets.Count == 0)
		{
			return new HrPolicyAssignmentResolution(null, [BuildDefaultStep("matched", "default_fallback")]);
		}

		var targetKeys = targets.Select(x => x.TargetKey).Distinct().ToList();
		var candidates = await ReadCandidatesAsync(academicYearId, date, targetKeys, cancellationToken);

		// Overlay a detached draft without changing the persisted candidates or the resolution algorithm.
		if (replacedAssignmentId.HasValue)
			candidates = candidates.Where(x => x.Id != replacedAssignmentId.Value).ToList();
		if (proposedAssignment != null && proposedAssignment.TenantId == CurrentTenant.Id &&
			proposedAssignment.AcademicYearId == academicYearId && proposedAssignment.IsEffectiveOn(date) &&
			targets.Any(target => target.TargetType == proposedAssignment.TargetType && target.TargetKey == proposedAssignment.TargetKey))
			candidates.Add(proposedAssignment);

		var specificity = targets.ToDictionary(x => (x.TargetType, x.TargetKey), x => x.Specificity);
		var winner = candidates
			.Where(x => specificity.ContainsKey((x.TargetType, x.TargetKey)))
			.OrderByDescending(x => specificity[(x.TargetType, x.TargetKey)])
			.ThenByDescending(x => x.Priority)
			.ThenByDescending(x => x.EffectiveFrom)
			.ThenBy(x => x.Id)
			.FirstOrDefault();

		if (!includeSteps)
			return new HrPolicyAssignmentResolution(winner, []);

		var steps = BuildResolutionSteps(targets, candidates, winner);
		steps.Add(BuildDefaultStep(winner is null ? "matched" : "skipped", winner is null ? "default_fallback" : "lower_specificity"));
		return new HrPolicyAssignmentResolution(winner, steps);
	}

	private async Task<List<HrPolicyAssignment>> ReadCandidatesAsync(
		Guid year, DateTime date, List<string> targetKeys, CancellationToken cancellationToken)
	{
		if (batchPeriod is { } period && batchAssignmentsByTarget != null)
		{
			if (!batchAssignmentsByTarget.TryGetValue(year, out var byTarget))
			{
				var query = await assignmentRepository.GetQueryableAsync();
				var assignments = await AsyncExecuter.ToListAsync(query.Where(x =>
					x.AcademicYearId == year && x.IsActive && x.EffectiveFrom <= period.End &&
					(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= period.Start)), cancellationToken);
				batchAssignmentsByTarget[year] = byTarget = assignments.ToLookup(x => x.TargetKey);
			}
			return targetKeys.SelectMany(key => byTarget[key])
				.Where(x => x.EffectiveFrom <= date && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= date))
				.ToList();
		}

		if (batchAssignments?.TryGetValue((year, date), out var datedAssignments) == true)
		{
			return datedAssignments.Where(x => targetKeys.Contains(x.TargetKey)).ToList();
		}

		var datedQuery = (await assignmentRepository.GetQueryableAsync()).Where(x =>
			x.AcademicYearId == year && x.IsActive && x.EffectiveFrom <= date &&
			(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= date));
		if (batchAssignments == null)
		{
			return await AsyncExecuter.ToListAsync(datedQuery.Where(x => targetKeys.Contains(x.TargetKey)), cancellationToken);
		}

		var all = await AsyncExecuter.ToListAsync(datedQuery, cancellationToken);
		batchAssignments[(year, date)] = all;
		return all.Where(x => targetKeys.Contains(x.TargetKey)).ToList();
	}

	private async Task<List<PolicyTargetKey>> GetTargetKeysAsync(
		Employee? employee, Guid? groupId, Guid year, CancellationToken cancellationToken)
	{
		if (batchTargets == null || employee == null)
			return await BuildTargetKeysAsync(employee, groupId, year, cancellationToken);

		var key = (year, employee.Id);
		if (!batchTargets.TryGetValue(key, out var targets))
			batchTargets[key] = targets = await BuildTargetKeysAsync(employee, groupId, year, cancellationToken);
		return targets;
	}

	private async Task<List<PolicyTargetKey>> BuildTargetKeysAsync(
		Employee? employee,
		Guid? groupId,
		Guid academicYearId,
		CancellationToken cancellationToken)
	{
		var targets = new List<PolicyTargetKey>();
		if (employee != null)
		{
			targets.Add(new PolicyTargetKey(HrPolicyAssignmentTargetType.Employee, employee.Id.ToString("D").ToLowerInvariant(), 100));
			if (batchGroups != null && !batchGroups.ContainsKey(academicYearId))
				batchGroups[academicYearId] = await groupRepository.GetListWithDetailsAsync(academicYearId, cancellationToken: cancellationToken);
			var groups = batchGroups != null ? batchGroups[academicYearId] : await groupRepository.GetListWithDetailsAsync(academicYearId, cancellationToken: cancellationToken);
			targets.AddRange(groups
				.Where(group => HrPolicyGroupMembership.MatchesEmployee(group, employee))
				.Select(group => new PolicyTargetKey(HrPolicyAssignmentTargetType.Group, group.Id.ToString("D").ToLowerInvariant(), 50)));
		}
		else if (groupId.HasValue)
		{
			targets.Add(new PolicyTargetKey(HrPolicyAssignmentTargetType.Group, groupId.Value.ToString("D").ToLowerInvariant(), 50));
		}

		return targets;
	}

	private static List<HrPolicyResolutionStep> BuildResolutionSteps(
		IReadOnlyList<PolicyTargetKey> targets,
		IReadOnlyList<HrPolicyAssignment> candidates,
		HrPolicyAssignment? winner)
	{
		var steps = new List<HrPolicyResolutionStep>();
		var orderedTargets = targets.OrderByDescending(x => x.Specificity).ToList();
		foreach (var target in orderedTargets)
		{
			var targetCandidates = candidates
				.Where(x => x.TargetType == target.TargetType && x.TargetKey == target.TargetKey)
				.OrderByDescending(x => x.Priority)
				.ThenByDescending(x => x.EffectiveFrom)
				.ThenBy(x => x.Id)
				.ToList();

			if (targetCandidates.Count == 0)
			{
				steps.Add(new HrPolicyResolutionStep(
					target.TargetType.ToString().ToLowerInvariant(),
					target.TargetType,
					target.TargetKey,
					null,
					null,
					null,
					"skipped",
					"no_assignment",
					null,
					null,
					null));
				continue;
			}

			foreach (var candidate in targetCandidates)
			{
				var isWinner = winner?.Id == candidate.Id;
				steps.Add(new HrPolicyResolutionStep(
					candidate.TargetType.ToString().ToLowerInvariant(),
					candidate.TargetType,
					candidate.TargetKey,
					candidate.TargetDisplayName,
					candidate.Id,
					candidate.Priority,
					isWinner ? "matched" : "skipped",
					GetResolutionReason(candidate, target.Specificity, winner, orderedTargets),
					candidate.PayrollPolicyId,
					candidate.AttendancePolicyId,
					candidate.LeavePolicyId));
			}
		}

		return steps;
	}

	private static string GetResolutionReason(
		HrPolicyAssignment candidate,
		int candidateSpecificity,
		HrPolicyAssignment? winner,
		IReadOnlyList<PolicyTargetKey> orderedTargets)
	{
		if (winner is null || winner.Id == candidate.Id)
		{
			return "winner";
		}

		var winnerSpecificity = orderedTargets
			.First(x => x.TargetType == winner.TargetType && x.TargetKey == winner.TargetKey)
			.Specificity;
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

	private static HrPolicyResolutionStep BuildDefaultStep(string status, string reason) =>
		new("default", null, null, "Academic year default", null, null, status, reason, null, null, null);

	private sealed record PolicyTargetKey(HrPolicyAssignmentTargetType TargetType, string TargetKey, int Specificity);
}

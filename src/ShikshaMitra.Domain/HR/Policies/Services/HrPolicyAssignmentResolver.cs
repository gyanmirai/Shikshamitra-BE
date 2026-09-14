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
	internal IDisposable BeginReadBatch()
	{
		var previousGroups = batchGroups;
		var previousAssignments = batchAssignments;
		batchGroups = new(); batchAssignments = new();
		return new Volo.Abp.DisposeAction(() => { batchGroups = previousGroups; batchAssignments = previousAssignments; });
	}
	internal async Task<HrPolicyAssignmentResolution> ResolveForEmployeeAsync(
		Employee employee,
		Guid academicYearId,
		DateTime date,
		CancellationToken cancellationToken)
	{
		return await ResolveAsync(employee, null, academicYearId, date, cancellationToken);
	}

	internal async Task<HrPolicyAssignmentResolution> ResolvePreviewAsync(
		Guid? groupId,
		Guid academicYearId,
		DateTime date,
		CancellationToken cancellationToken)
	{
		return await ResolveAsync(null, groupId, academicYearId, date, cancellationToken);
	}

	private async Task<HrPolicyAssignmentResolution> ResolveAsync(
		Employee? employee,
		Guid? groupId,
		Guid academicYearId,
		DateTime date,
		CancellationToken cancellationToken)
	{
		var targets = await BuildTargetKeysAsync(employee, groupId, academicYearId, cancellationToken);
		if (targets.Count == 0)
		{
			return new HrPolicyAssignmentResolution(null, [BuildDefaultStep("matched", "default_fallback")]);
		}

		var targetKeys = targets.Select(x => x.TargetKey).Distinct().ToList();
		var query = await assignmentRepository.GetQueryableAsync();
		if (batchAssignments != null && !batchAssignments.ContainsKey((academicYearId, date)))
		{
			batchAssignments[(academicYearId, date)] = await AsyncExecuter.ToListAsync(query.Where(x =>
				x.AcademicYearId == academicYearId && x.IsActive && x.EffectiveFrom <= date &&
				(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= date)), cancellationToken);
		}
		var candidates = batchAssignments != null
			? batchAssignments[(academicYearId, date)].Where(x => targetKeys.Contains(x.TargetKey)).ToList()
			: await AsyncExecuter.ToListAsync(query
			.Where(x =>
				x.AcademicYearId == academicYearId &&
				x.IsActive &&
				targetKeys.Contains(x.TargetKey) &&
				x.EffectiveFrom <= date &&
				(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= date)), cancellationToken);

		var specificity = targets.ToDictionary(x => (x.TargetType, x.TargetKey), x => x.Specificity);
		var winner = candidates
			.Where(x => specificity.ContainsKey((x.TargetType, x.TargetKey)))
			.OrderByDescending(x => specificity[(x.TargetType, x.TargetKey)])
			.ThenByDescending(x => x.Priority)
			.ThenByDescending(x => x.EffectiveFrom)
			.ThenBy(x => x.Id)
			.FirstOrDefault();

		var steps = BuildResolutionSteps(targets, candidates, winner);
		steps.Add(BuildDefaultStep(winner is null ? "matched" : "skipped", winner is null ? "default_fallback" : "lower_specificity"));
		return new HrPolicyAssignmentResolution(winner, steps);
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

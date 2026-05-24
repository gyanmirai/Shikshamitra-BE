using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.Policies;

public sealed record HrResolvedPolicies(
	Guid EmployeeId,
	Guid AcademicYearId,
	DateTime Date,
	HrPolicyAssignment? Assignment,
	string PolicySource,
	WorkCalendarPolicy PayrollWorkCalendarPolicy,
	WorkCalendarPolicy AttendanceWorkCalendarPolicy,
	PayrollPolicy PayrollPolicy,
	AttendancePolicy AttendancePolicy,
	IReadOnlyList<HrPolicyResolutionStep> ResolutionSteps);

public sealed record HrPolicyResolutionStep(
	string Source,
	HrPolicyAssignmentTargetType? TargetType,
	string? TargetKey,
	string? TargetDisplayName,
	Guid? AssignmentId,
	int? Priority,
	string Status,
	string SkipReason,
	Guid? PayrollPolicyId,
	Guid? AttendancePolicyId);

public class HrPolicyResolver(
	IRepository<HrPolicyAssignment, Guid> assignmentRepository,
	IRepository<Employee, Guid> employeeRepository,
	IGroupRepository groupRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	IRepository<WorkCalendarPolicy, Guid> workCalendarPolicyRepository)
	: DomainService
{
	public async Task<HrResolvedPolicies> ResolveAsync(
		Guid employeeId,
		DateTime date,
		Guid? academicYearId = null,
		CancellationToken cancellationToken = default)
	{
		var resolvedAcademicYearId = academicYearId ?? await ResolveAcademicYearIdAsync(date, cancellationToken);
		var targetDate = date.Date;
		var employee = await employeeRepository.GetAsync(employeeId, cancellationToken: cancellationToken);
		var resolution = await ResolveAssignmentAsync(
			employee,
			resolvedAcademicYearId,
			targetDate,
			cancellationToken);
		var assignment = resolution.Assignment;
		var payrollPolicy = assignment is null
			? await ResolveDefaultPayrollPolicyAsync(resolvedAcademicYearId, cancellationToken)
			: await payrollPolicyRepository.GetAsync(assignment.PayrollPolicyId, cancellationToken: cancellationToken);
		var attendancePolicy = assignment is null
			? await ResolveDefaultAttendancePolicyAsync(resolvedAcademicYearId, cancellationToken)
			: await attendancePolicyRepository.GetAsync(assignment.AttendancePolicyId, cancellationToken: cancellationToken);

		EnsureActive(payrollPolicy.Status, "ShikshaMitra:DefaultPayrollPolicyRequired");
		EnsureActive(attendancePolicy.Status, "ShikshaMitra:DefaultAttendancePolicyRequired");
		var payrollWorkCalendar = await workCalendarPolicyRepository.GetAsync(payrollPolicy.WorkCalendarPolicyId, cancellationToken: cancellationToken);
		var attendanceWorkCalendar = await workCalendarPolicyRepository.GetAsync(attendancePolicy.WorkCalendarPolicyId, cancellationToken: cancellationToken);
		EnsureActive(payrollWorkCalendar.Status, "ShikshaMitra:WorkCalendarPolicyMustBeActive");
		EnsureActive(attendanceWorkCalendar.Status, "ShikshaMitra:WorkCalendarPolicyMustBeActive");

		return new HrResolvedPolicies(
			employeeId,
			resolvedAcademicYearId,
			targetDate,
			assignment,
			assignment is null ? "default" : assignment.TargetType.ToString().ToLowerInvariant(),
			payrollWorkCalendar,
			attendanceWorkCalendar,
			payrollPolicy,
			attendancePolicy,
			resolution.Steps);
	}

	public async Task<HrResolvedPolicies> ResolvePreviewAsync(
		Guid? employeeId,
		Guid? groupId,
		DateTime date,
		Guid academicYearId,
		CancellationToken cancellationToken = default)
	{
		var targetDate = date.Date;
		if (employeeId.HasValue)
		{
			return await ResolveAsync(employeeId.Value, targetDate, academicYearId, cancellationToken);
		}

		var resolution = await ResolveAssignmentAsync(null, groupId, academicYearId, targetDate, cancellationToken);
		var assignment = resolution.Assignment;
		var payrollPolicy = assignment is null
			? await ResolveDefaultPayrollPolicyAsync(academicYearId, cancellationToken)
			: await payrollPolicyRepository.GetAsync(assignment.PayrollPolicyId, cancellationToken: cancellationToken);
		var attendancePolicy = assignment is null
			? await ResolveDefaultAttendancePolicyAsync(academicYearId, cancellationToken)
			: await attendancePolicyRepository.GetAsync(assignment.AttendancePolicyId, cancellationToken: cancellationToken);

		EnsureActive(payrollPolicy.Status, "ShikshaMitra:DefaultPayrollPolicyRequired");
		EnsureActive(attendancePolicy.Status, "ShikshaMitra:DefaultAttendancePolicyRequired");
		var payrollWorkCalendar = await workCalendarPolicyRepository.GetAsync(payrollPolicy.WorkCalendarPolicyId, cancellationToken: cancellationToken);
		var attendanceWorkCalendar = await workCalendarPolicyRepository.GetAsync(attendancePolicy.WorkCalendarPolicyId, cancellationToken: cancellationToken);
		EnsureActive(payrollWorkCalendar.Status, "ShikshaMitra:WorkCalendarPolicyMustBeActive");
		EnsureActive(attendanceWorkCalendar.Status, "ShikshaMitra:WorkCalendarPolicyMustBeActive");

		return new HrResolvedPolicies(
			employeeId ?? Guid.Empty,
			academicYearId,
			targetDate,
			assignment,
			assignment is null ? "default" : assignment.TargetType.ToString(),
			payrollWorkCalendar,
			attendanceWorkCalendar,
			payrollPolicy,
			attendancePolicy,
			resolution.Steps);
	}

	private async Task<Guid> ResolveAcademicYearIdAsync(DateTime date, CancellationToken cancellationToken)
	{
		var targetDate = date.Date;
		var query = await academicYearRepository.GetQueryableAsync();
		var academicYear = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x =>
			x.StartDate <= targetDate &&
			x.EndDate >= targetDate), cancellationToken);

		return academicYear?.Id ?? throw new BusinessException("ShikshaMitra:AcademicYearRequiredForPolicyResolution");
	}

	private async Task<HrPolicyAssignmentResolution> ResolveAssignmentAsync(
		Employee? employee,
		Guid academicYearId,
		DateTime date,
		CancellationToken cancellationToken)
	{
		var targets = await BuildTargetKeysAsync(employee, null, academicYearId, cancellationToken);
		if (targets.Count == 0)
		{
			return new HrPolicyAssignmentResolution(null, [BuildDefaultStep("matched", "default_fallback")]);
		}

		var targetKeys = targets.Select(x => x.TargetKey).Distinct().ToList();
		var query = await assignmentRepository.GetQueryableAsync();
		var candidates = await AsyncExecuter.ToListAsync(query
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
			.FirstOrDefault();

		var steps = BuildResolutionSteps(targets, candidates, winner);
		steps.Add(BuildDefaultStep(winner is null ? "matched" : "skipped", winner is null ? "default_fallback" : "lower_specificity"));
		return new HrPolicyAssignmentResolution(winner, steps);
	}

	private async Task<HrPolicyAssignmentResolution> ResolveAssignmentAsync(
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
		var candidates = await AsyncExecuter.ToListAsync(query
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
			var groups = await groupRepository.GetListWithDetailsAsync(academicYearId, cancellationToken: cancellationToken);
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

	private sealed record PolicyTargetKey(HrPolicyAssignmentTargetType TargetType, string TargetKey, int Specificity);
	private sealed record HrPolicyAssignmentResolution(HrPolicyAssignment? Assignment, IReadOnlyList<HrPolicyResolutionStep> Steps);

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
					isWinner
						? "winner"
						: target.Specificity < (winner is null ? 0 : orderedTargets.First(x => x.TargetType == winner.TargetType && x.TargetKey == winner.TargetKey).Specificity)
							? "lower_specificity"
							: "lower_priority",
					candidate.PayrollPolicyId,
					candidate.AttendancePolicyId));
			}
		}

		return steps;
	}

	private static HrPolicyResolutionStep BuildDefaultStep(string status, string reason) =>
		new("default", null, null, "Academic year default", null, null, status, reason, null, null);

	private async Task<PayrollPolicy> ResolveDefaultPayrollPolicyAsync(Guid academicYearId, CancellationToken cancellationToken)
	{
		var query = await payrollPolicyRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(x =>
			x.AcademicYearId == academicYearId &&
			x.IsDefault &&
			x.Status == HrPolicyStatus.Active), cancellationToken)
			?? throw new BusinessException("ShikshaMitra:DefaultPayrollPolicyRequired");
	}

	private async Task<AttendancePolicy> ResolveDefaultAttendancePolicyAsync(Guid academicYearId, CancellationToken cancellationToken)
	{
		var query = await attendancePolicyRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(x =>
			x.AcademicYearId == academicYearId &&
			x.IsDefault &&
			x.Status == HrPolicyStatus.Active), cancellationToken)
			?? throw new BusinessException("ShikshaMitra:DefaultAttendancePolicyRequired");
	}

	private static void EnsureActive(HrPolicyStatus status, string errorCode)
	{
		if (status != HrPolicyStatus.Active)
		{
			throw new BusinessException(errorCode);
		}
	}
}

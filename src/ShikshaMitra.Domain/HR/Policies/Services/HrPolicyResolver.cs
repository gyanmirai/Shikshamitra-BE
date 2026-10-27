using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
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
	LeavePolicy? LeavePolicy,
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
	Guid? AttendancePolicyId,
	Guid? LeavePolicyId);

public class HrPolicyResolver(
	IRepository<Employee, Guid> employeeRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	ILeavePolicyRepository leavePolicyRepository,
	IRepository<WorkCalendarPolicy, Guid> workCalendarPolicyRepository,
	HrPolicyAssignmentResolver assignmentResolver)
	: DomainService
{
	private Dictionary<string, object?>? readBatch;
	public IDisposable BeginReadBatch(IEnumerable<Employee> employees)
	{
		var previous = readBatch;
		readBatch = employees.ToDictionary(x => $"employee:{x.Id}", x => (object?) x);
		var assignments = assignmentResolver.BeginReadBatch();
		return new Volo.Abp.DisposeAction(() => { readBatch = previous; assignments.Dispose(); });
	}
	private async Task<T> ReadAsync<T>(string key, Func<Task<T>> load)
	{
		if (readBatch?.TryGetValue(key, out var cached) == true) return (T) cached!;
		var result = await load();
		if (readBatch != null) readBatch[key] = result;
		return result;
	}
	public async Task<HrResolvedPolicies> ResolveAsync(
		Guid employeeId,
		DateTime date,
		Guid? academicYearId = null,
		CancellationToken cancellationToken = default,
		HrPolicyAssignment? proposedAssignment = null,
		Guid? replacedAssignmentId = null)
	{
		var resolvedAcademicYearId = academicYearId ?? await ResolveAcademicYearIdAsync(date, cancellationToken);
		var targetDate = date.Date;
		var employee = await ReadAsync($"employee:{employeeId}", () => employeeRepository.GetAsync(employeeId, cancellationToken: cancellationToken));
		var resolution = await assignmentResolver.ResolveForEmployeeAsync(
			employee,
			resolvedAcademicYearId,
			targetDate,
			cancellationToken, proposedAssignment, replacedAssignmentId);
		var assignment = resolution.Assignment;
		var payrollPolicy = assignment is null
			? await ReadAsync($"default-payroll:{resolvedAcademicYearId}", () => ResolveDefaultPayrollPolicyAsync(resolvedAcademicYearId, cancellationToken))
			: await ReadAsync($"payroll:{assignment.PayrollPolicyId}", () => payrollPolicyRepository.GetAsync(assignment.PayrollPolicyId, cancellationToken: cancellationToken));
		var attendancePolicy = assignment is null
			? await ReadAsync($"default-attendance:{resolvedAcademicYearId}", () => ResolveDefaultAttendancePolicyAsync(resolvedAcademicYearId, cancellationToken))
			: await ReadAsync($"attendance:{assignment.AttendancePolicyId}", () => attendancePolicyRepository.GetAsync(assignment.AttendancePolicyId, cancellationToken: cancellationToken));
		var leavePolicy = assignment?.LeavePolicyId is Guid leavePolicyId
			? await ReadAsync($"leave:{leavePolicyId}", () => leavePolicyRepository.GetWithRulesAsync(leavePolicyId, cancellationToken))
			: await ReadAsync($"default-leave:{resolvedAcademicYearId}", () => ResolveDefaultLeavePolicyAsync(resolvedAcademicYearId, cancellationToken));

		EnsurePolicyUsable(payrollPolicy.AcademicYearId, payrollPolicy.Status, resolvedAcademicYearId, HRErrorCodes.DefaultPayrollPolicyRequired);
		EnsurePolicyUsable(attendancePolicy.AcademicYearId, attendancePolicy.Status, resolvedAcademicYearId, HRErrorCodes.DefaultAttendancePolicyRequired);
		EnsureLeavePolicyUsable(leavePolicy, resolvedAcademicYearId);
		var payrollWorkCalendar = await ReadAsync($"calendar:{payrollPolicy.WorkCalendarPolicyId}", () => workCalendarPolicyRepository.GetAsync(payrollPolicy.WorkCalendarPolicyId, cancellationToken: cancellationToken));
		var attendanceWorkCalendar = await ReadAsync($"calendar:{attendancePolicy.WorkCalendarPolicyId}", () => workCalendarPolicyRepository.GetAsync(attendancePolicy.WorkCalendarPolicyId, cancellationToken: cancellationToken));
		EnsureWorkCalendarUsable(payrollWorkCalendar.AcademicYearId, payrollWorkCalendar.Status, resolvedAcademicYearId);
		EnsureWorkCalendarUsable(attendanceWorkCalendar.AcademicYearId, attendanceWorkCalendar.Status, resolvedAcademicYearId);

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
			leavePolicy,
			resolution.Steps);
	}

	public async Task<HrResolvedPolicies> ResolvePreviewAsync(
		Guid? employeeId,
		Guid? groupId,
		DateTime date,
		Guid academicYearId,
		CancellationToken cancellationToken = default,
		HrPolicyAssignment? proposedAssignment = null,
		Guid? replacedAssignmentId = null)
	{
		var targetDate = date.Date;
		if (employeeId.HasValue)
		{
			return await ResolveAsync(employeeId.Value, targetDate, academicYearId, cancellationToken, proposedAssignment, replacedAssignmentId);
		}

		var resolution = await assignmentResolver.ResolvePreviewAsync(groupId, academicYearId, targetDate, cancellationToken, proposedAssignment, replacedAssignmentId);
		var assignment = resolution.Assignment;
		var payrollPolicy = assignment is null
			? await ResolveDefaultPayrollPolicyAsync(academicYearId, cancellationToken)
			: await payrollPolicyRepository.GetAsync(assignment.PayrollPolicyId, cancellationToken: cancellationToken);
		var attendancePolicy = assignment is null
			? await ResolveDefaultAttendancePolicyAsync(academicYearId, cancellationToken)
			: await attendancePolicyRepository.GetAsync(assignment.AttendancePolicyId, cancellationToken: cancellationToken);
		var leavePolicy = assignment?.LeavePolicyId is Guid leavePolicyId
			? await leavePolicyRepository.GetWithRulesAsync(leavePolicyId, cancellationToken)
			: await ResolveDefaultLeavePolicyAsync(academicYearId, cancellationToken);

		EnsurePolicyUsable(payrollPolicy.AcademicYearId, payrollPolicy.Status, academicYearId, HRErrorCodes.DefaultPayrollPolicyRequired);
		EnsurePolicyUsable(attendancePolicy.AcademicYearId, attendancePolicy.Status, academicYearId, HRErrorCodes.DefaultAttendancePolicyRequired);
		EnsureLeavePolicyUsable(leavePolicy, academicYearId);
		var payrollWorkCalendar = await workCalendarPolicyRepository.GetAsync(payrollPolicy.WorkCalendarPolicyId, cancellationToken: cancellationToken);
		var attendanceWorkCalendar = await workCalendarPolicyRepository.GetAsync(attendancePolicy.WorkCalendarPolicyId, cancellationToken: cancellationToken);
		EnsureWorkCalendarUsable(payrollWorkCalendar.AcademicYearId, payrollWorkCalendar.Status, academicYearId);
		EnsureWorkCalendarUsable(attendanceWorkCalendar.AcademicYearId, attendanceWorkCalendar.Status, academicYearId);

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
			leavePolicy,
			resolution.Steps);
	}

	private async Task<Guid> ResolveAcademicYearIdAsync(DateTime date, CancellationToken cancellationToken)
	{
		var targetDate = date.Date;
		var query = await academicYearRepository.GetQueryableAsync();
		var academicYear = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x =>
			x.StartDate <= targetDate &&
			x.EndDate >= targetDate), cancellationToken);

		return academicYear?.Id ?? throw new BusinessException(HRErrorCodes.AcademicYearRequiredForPolicyResolution);
	}

	private async Task<PayrollPolicy> ResolveDefaultPayrollPolicyAsync(Guid academicYearId, CancellationToken cancellationToken)
	{
		var query = await payrollPolicyRepository.GetQueryableAsync();
		var defaults = await AsyncExecuter.ToListAsync(query.Where(x =>
			x.AcademicYearId == academicYearId &&
			x.IsDefault &&
			x.Status == HrPolicyStatus.Active), cancellationToken);

		if (defaults.Count != 1)
		{
			throw new BusinessException(HRErrorCodes.DefaultPayrollPolicyRequired)
				.WithData("defaultCount", defaults.Count);
		}

		return defaults[0];
	}

	private async Task<AttendancePolicy> ResolveDefaultAttendancePolicyAsync(Guid academicYearId, CancellationToken cancellationToken)
	{
		var query = await attendancePolicyRepository.GetQueryableAsync();
		var defaults = await AsyncExecuter.ToListAsync(query.Where(x =>
			x.AcademicYearId == academicYearId &&
			x.IsDefault &&
			x.Status == HrPolicyStatus.Active), cancellationToken);

		if (defaults.Count != 1)
		{
			throw new BusinessException(HRErrorCodes.DefaultAttendancePolicyRequired)
				.WithData("defaultCount", defaults.Count);
		}

		return defaults[0];
	}

	private async Task<LeavePolicy?> ResolveDefaultLeavePolicyAsync(Guid academicYearId, CancellationToken cancellationToken)
	{
		var policies = await leavePolicyRepository.GetListByAcademicYearIdAsync(
			academicYearId,
			includeInactive: false,
			cancellationToken: cancellationToken);
		var defaults = policies
			.Where(x => x.Status == HrPolicyStatus.Active && x.IsActive && x.IsDefault)
			.OrderBy(x => x.PolicyName)
			.ThenBy(x => x.Id)
			.ToList();

		if (defaults.Count != 1)
		{
			throw new BusinessException(HRErrorCodes.DefaultLeavePolicyRequired)
				.WithData("defaultCount", defaults.Count);
		}

		return defaults[0];
	}

	private static void EnsurePolicyUsable(Guid policyAcademicYearId, HrPolicyStatus status, Guid academicYearId, string inactiveErrorCode)
	{
		if (policyAcademicYearId != academicYearId)
		{
			throw new BusinessException(HRErrorCodes.HrPolicyAssignmentAcademicYearMismatch);
		}

		if (status != HrPolicyStatus.Active)
		{
			throw new BusinessException(inactiveErrorCode);
		}
	}

	private static void EnsureWorkCalendarUsable(Guid workCalendarAcademicYearId, HrPolicyStatus status, Guid academicYearId)
	{
		if (workCalendarAcademicYearId != academicYearId)
		{
			throw new BusinessException(HRErrorCodes.HrPolicyInvalidWorkCalendar);
		}

		if (status != HrPolicyStatus.Active)
		{
			throw new BusinessException(HRErrorCodes.WorkCalendarPolicyMustBeActive);
		}
	}

	private static void EnsureLeavePolicyUsable(LeavePolicy? policy, Guid academicYearId)
	{
		if (policy == null)
		{
			throw new BusinessException(HRErrorCodes.DefaultLeavePolicyRequired);
		}

		if (policy.AcademicYearId != academicYearId)
		{
			throw new BusinessException(HRErrorCodes.HrPolicyAssignmentAcademicYearMismatch);
		}

		if (policy.Status != HrPolicyStatus.Active || !policy.IsActive)
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyInactive);
		}
	}
}
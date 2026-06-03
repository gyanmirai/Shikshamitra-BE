using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
	IRepository<Employee, Guid> employeeRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	IRepository<WorkCalendarPolicy, Guid> workCalendarPolicyRepository,
	HrPolicyAssignmentResolver assignmentResolver)
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
		var resolution = await assignmentResolver.ResolveForEmployeeAsync(
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

		EnsureActive(payrollPolicy.Status, HRErrorCodes.DefaultPayrollPolicyRequired);
		EnsureActive(attendancePolicy.Status, HRErrorCodes.DefaultAttendancePolicyRequired);
		var payrollWorkCalendar = await workCalendarPolicyRepository.GetAsync(payrollPolicy.WorkCalendarPolicyId, cancellationToken: cancellationToken);
		var attendanceWorkCalendar = await workCalendarPolicyRepository.GetAsync(attendancePolicy.WorkCalendarPolicyId, cancellationToken: cancellationToken);
		EnsureActive(payrollWorkCalendar.Status, HRErrorCodes.WorkCalendarPolicyMustBeActive);
		EnsureActive(attendanceWorkCalendar.Status, HRErrorCodes.WorkCalendarPolicyMustBeActive);

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

		var resolution = await assignmentResolver.ResolvePreviewAsync(groupId, academicYearId, targetDate, cancellationToken);
		var assignment = resolution.Assignment;
		var payrollPolicy = assignment is null
			? await ResolveDefaultPayrollPolicyAsync(academicYearId, cancellationToken)
			: await payrollPolicyRepository.GetAsync(assignment.PayrollPolicyId, cancellationToken: cancellationToken);
		var attendancePolicy = assignment is null
			? await ResolveDefaultAttendancePolicyAsync(academicYearId, cancellationToken)
			: await attendancePolicyRepository.GetAsync(assignment.AttendancePolicyId, cancellationToken: cancellationToken);

		EnsureActive(payrollPolicy.Status, HRErrorCodes.DefaultPayrollPolicyRequired);
		EnsureActive(attendancePolicy.Status, HRErrorCodes.DefaultAttendancePolicyRequired);
		var payrollWorkCalendar = await workCalendarPolicyRepository.GetAsync(payrollPolicy.WorkCalendarPolicyId, cancellationToken: cancellationToken);
		var attendanceWorkCalendar = await workCalendarPolicyRepository.GetAsync(attendancePolicy.WorkCalendarPolicyId, cancellationToken: cancellationToken);
		EnsureActive(payrollWorkCalendar.Status, HRErrorCodes.WorkCalendarPolicyMustBeActive);
		EnsureActive(attendanceWorkCalendar.Status, HRErrorCodes.WorkCalendarPolicyMustBeActive);

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

		return academicYear?.Id ?? throw new BusinessException(HRErrorCodes.AcademicYearRequiredForPolicyResolution);
	}

	private async Task<PayrollPolicy> ResolveDefaultPayrollPolicyAsync(Guid academicYearId, CancellationToken cancellationToken)
	{
		var query = await payrollPolicyRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(x =>
			x.AcademicYearId == academicYearId &&
			x.IsDefault &&
			x.Status == HrPolicyStatus.Active), cancellationToken)
			?? throw new BusinessException(HRErrorCodes.DefaultPayrollPolicyRequired);
	}

	private async Task<AttendancePolicy> ResolveDefaultAttendancePolicyAsync(Guid academicYearId, CancellationToken cancellationToken)
	{
		var query = await attendancePolicyRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(x =>
			x.AcademicYearId == academicYearId &&
			x.IsDefault &&
			x.Status == HrPolicyStatus.Active), cancellationToken)
			?? throw new BusinessException(HRErrorCodes.DefaultAttendancePolicyRequired);
	}

	private static void EnsureActive(HrPolicyStatus status, string errorCode)
	{
		if (status != HrPolicyStatus.Active)
		{
			throw new BusinessException(errorCode);
		}
	}
}

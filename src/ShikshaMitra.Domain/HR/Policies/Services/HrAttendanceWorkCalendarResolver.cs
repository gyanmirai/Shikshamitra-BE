using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.Policies;

/// <summary>
/// Resolves the attendance work calendar without requiring unrelated payroll or leave policies.
/// </summary>
public class HrAttendanceWorkCalendarResolver(
	IRepository<Employee, Guid> employeeRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	IRepository<WorkCalendarPolicy, Guid> workCalendarPolicyRepository,
	HrPolicyAssignmentResolver assignmentResolver)
	: DomainService
{
	public async Task<WorkCalendarPolicy> ResolveAsync(
		Guid employeeId,
		DateTime date,
		Guid? academicYearId = null,
		CancellationToken cancellationToken = default)
	{
		var resolvedAcademicYearId = academicYearId ??
			await ResolveAcademicYearIdAsync(date, cancellationToken);
		var employee = await employeeRepository.GetAsync(
			employeeId,
			cancellationToken: cancellationToken);
		var resolution = await assignmentResolver.ResolveForEmployeeAsync(
			employee,
			resolvedAcademicYearId,
			date.Date,
			cancellationToken);
		var attendancePolicy = resolution.Assignment is null
			? await ResolveDefaultAttendancePolicyAsync(resolvedAcademicYearId, cancellationToken)
			: await attendancePolicyRepository.GetAsync(
				resolution.Assignment.AttendancePolicyId,
				cancellationToken: cancellationToken);

		EnsureAttendancePolicyUsable(attendancePolicy, resolvedAcademicYearId);
		var workCalendar = await workCalendarPolicyRepository.GetAsync(
			attendancePolicy.WorkCalendarPolicyId,
			cancellationToken: cancellationToken);
		EnsureWorkCalendarUsable(workCalendar, resolvedAcademicYearId);
		return workCalendar;
	}

	private async Task<Guid> ResolveAcademicYearIdAsync(
		DateTime date,
		CancellationToken cancellationToken)
	{
		var targetDate = date.Date;
		var query = await academicYearRepository.GetQueryableAsync();
		var academicYear = await AsyncExecuter.FirstOrDefaultAsync(
			query.Where(year =>
				year.StartDate <= targetDate &&
				year.EndDate >= targetDate),
			cancellationToken);
		return academicYear?.Id ??
			throw new BusinessException(HRErrorCodes.AcademicYearRequiredForPolicyResolution);
	}

	private async Task<AttendancePolicy> ResolveDefaultAttendancePolicyAsync(
		Guid academicYearId,
		CancellationToken cancellationToken)
	{
		var query = await attendancePolicyRepository.GetQueryableAsync();
		var defaults = await AsyncExecuter.ToListAsync(
			query.Where(policy =>
				policy.AcademicYearId == academicYearId &&
				policy.IsDefault &&
				policy.Status == HrPolicyStatus.Active),
			cancellationToken);
		if (defaults.Count != 1)
		{
			throw new BusinessException(HRErrorCodes.DefaultAttendancePolicyRequired)
				.WithData("defaultCount", defaults.Count);
		}

		return defaults[0];
	}

	private static void EnsureAttendancePolicyUsable(
		AttendancePolicy policy,
		Guid academicYearId)
	{
		if (policy.AcademicYearId != academicYearId)
		{
			throw new BusinessException(HRErrorCodes.HrPolicyAssignmentAcademicYearMismatch);
		}

		if (policy.Status != HrPolicyStatus.Active)
		{
			throw new BusinessException(HRErrorCodes.DefaultAttendancePolicyRequired);
		}
	}

	private static void EnsureWorkCalendarUsable(
		WorkCalendarPolicy workCalendar,
		Guid academicYearId)
	{
		if (workCalendar.AcademicYearId != academicYearId)
		{
			throw new BusinessException(HRErrorCodes.HrPolicyInvalidWorkCalendar);
		}

		if (workCalendar.Status != HrPolicyStatus.Active)
		{
			throw new BusinessException(HRErrorCodes.WorkCalendarPolicyMustBeActive);
		}
	}
}

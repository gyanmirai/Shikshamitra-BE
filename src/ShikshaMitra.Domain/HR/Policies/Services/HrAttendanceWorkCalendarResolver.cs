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
	private Dictionary<string, object>? readBatch;
	private List<AcademicYear>? batchYears;

	public async Task<IDisposable> BeginReadBatchAsync(
		IEnumerable<Employee> employees, DateOnly start, DateOnly end)
	{
		var previous = readBatch;
		var previousYears = batchYears;
		var firstDay = start.ToDateTime(TimeOnly.MinValue);
		var lastDay = end.ToDateTime(TimeOnly.MinValue);
		var years = await academicYearRepository.GetQueryableAsync();
		batchYears = await AsyncExecuter.ToListAsync(years
			.Where(year => year.StartDate <= lastDay && year.EndDate >= firstDay)
			.OrderByDescending(year => year.StartDate).ThenBy(year => year.Id));
		readBatch = employees.ToDictionary(x => "employee:" + x.Id, x => (object) Task.FromResult(x));
		var assignments = assignmentResolver.BeginReadBatch((firstDay, lastDay));
		return new DisposeAction(() =>
		{
			readBatch = previous;
			batchYears = previousYears;
			assignments.Dispose();
		});
	}

	private Task<T> ReadAsync<T>(string key, Func<Task<T>> load) where T : notnull
	{
		if (readBatch == null) return load();
		// Cache failed configuration reads for this request too. The register can use
		// its existing holiday fallback without repeating the same database query.
		if (!readBatch.TryGetValue(key, out var value))
		{
			value = load();
			readBatch.Add(key, value);
		}
		return (Task<T>) value;
	}
	public async Task<WorkCalendarPolicy> ResolveAsync(
		Guid employeeId,
		DateTime date,
		Guid? academicYearId = null,
		CancellationToken cancellationToken = default)
	{
		var resolvedAcademicYearId = academicYearId ??
			await ReadAsync("year:" + date.Date.ToString("yyyy-MM-dd"), () => ResolveAcademicYearIdAsync(date, cancellationToken));
		var employee = await ReadAsync("employee:" + employeeId, () => employeeRepository.GetAsync(employeeId, cancellationToken: cancellationToken));
		var resolution = await assignmentResolver.ResolveForEmployeeAsync(
			employee,
			resolvedAcademicYearId,
			date.Date,
			cancellationToken,
			includeSteps: false);
		var attendancePolicy = resolution.Assignment is null
			? await ReadAsync("default:" + resolvedAcademicYearId, () => ResolveDefaultAttendancePolicyAsync(resolvedAcademicYearId, cancellationToken))
			: await ReadAsync("policy:" + resolution.Assignment.AttendancePolicyId, () => attendancePolicyRepository.GetAsync(resolution.Assignment.AttendancePolicyId, cancellationToken: cancellationToken));

		EnsureAttendancePolicyUsable(attendancePolicy, resolvedAcademicYearId);
		var workCalendar = await ReadAsync("calendar:" + attendancePolicy.WorkCalendarPolicyId, () => workCalendarPolicyRepository.GetAsync(attendancePolicy.WorkCalendarPolicyId, cancellationToken: cancellationToken));
		EnsureWorkCalendarUsable(workCalendar, resolvedAcademicYearId);
		return workCalendar;
	}

	private async Task<Guid> ResolveAcademicYearIdAsync(
		DateTime date,
		CancellationToken cancellationToken)
	{
		var targetDate = date.Date;
		if (batchYears != null)
		{
			return batchYears.FirstOrDefault(year => year.StartDate <= targetDate && year.EndDate >= targetDate)?.Id ??
				throw new BusinessException(HRErrorCodes.AcademicYearRequiredForPolicyResolution);
		}

		var query = await academicYearRepository.GetQueryableAsync();
		var academicYear = await AsyncExecuter.FirstOrDefaultAsync(
			query.Where(year =>
				year.StartDate <= targetDate &&
				year.EndDate >= targetDate)
				.OrderByDescending(year => year.StartDate).ThenBy(year => year.Id),
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

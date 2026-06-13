using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class AttendanceCaptureModeReadLookupReader(
	IRepository<AcademicYear, Guid> academicYearRepository,
	IRepository<Employee, Guid> employeeRepository,
	IRepository<HrPolicyAssignment, Guid> assignmentRepository,
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	IGroupRepository groupRepository,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task<Dictionary<Guid, AttendanceCaptureMode>> ResolveForDateAsync(
		IEnumerable<Guid> employeeIds,
		DateOnly date,
		AttendanceCaptureMode fallback = AttendanceCaptureMode.Integrated)
	{
		var lookup = await ResolveForDatesAsync(employeeIds, [date], fallback);
		return lookup.ToDictionary(x => x.Key.EmployeeId, x => x.Value);
	}

	public async Task<Dictionary<(Guid EmployeeId, DateOnly Date), AttendanceCaptureMode>> ResolveForDatesAsync(
		IEnumerable<Guid> employeeIds,
		IEnumerable<DateOnly> dates,
		AttendanceCaptureMode fallback = AttendanceCaptureMode.Integrated)
	{
		var ids = employeeIds.Distinct().ToList();
		var days = dates.Distinct().OrderBy(x => x).ToList();
		var lookup = AttendanceCaptureModeReadLookup.BuildFallbackLookup(ids, days, fallback);
		if (ids.Count == 0 || days.Count == 0)
		{
			return lookup;
		}

		var years = await LoadAcademicYearsAsync(days);
		var yearByDate = days.ToDictionary(x => x, x => ResolveAcademicYearId(x, years));
		var yearIds = yearByDate.Values.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
		if (yearIds.Count == 0)
		{
			return lookup;
		}

		var employees = await LoadEmployeesAsync(ids);
		if (employees.Count == 0)
		{
			return lookup;
		}

		var groupsByYear = await LoadGroupsByYearAsync(yearIds);
		var groupKeysByEmployeeYear = AttendanceCaptureModeReadLookup.BuildGroupKeysByEmployeeYear(employees, groupsByYear);
		var targetKeys = AttendanceCaptureModeReadLookup.BuildTargetKeys(ids, groupKeysByEmployeeYear);
		var assignments = targetKeys.Count == 0
			? []
			: await LoadAssignmentsAsync(yearIds, targetKeys, days.First(), days.Last());
		var policyLookup = await LoadPolicyModesAsync(yearIds, assignments.Select(x => x.AttendancePolicyId));

		foreach (var employee in employees)
		{
			foreach (var date in days)
			{
				if (!yearByDate.TryGetValue(date, out var academicYearId) || !academicYearId.HasValue)
				{
					continue;
				}

				var assignment = AttendanceCaptureModeReadLookup.SelectAssignment(employee.Id, academicYearId.Value, date, assignments, groupKeysByEmployeeYear);
				lookup[(employee.Id, date)] = AttendanceCaptureModeReadLookup.ResolveCaptureMode(assignment, academicYearId.Value, policyLookup, fallback);
			}
		}

		return lookup;
	}

	private async Task<List<AcademicYear>> LoadAcademicYearsAsync(List<DateOnly> days)
	{
		var minDate = days.First().ToDateTime(TimeOnly.MinValue);
		var maxDate = days.Last().ToDateTime(TimeOnly.MinValue);
		var query = await academicYearRepository.GetQueryableAsync();
		return await asyncExecuter.ToListAsync(query.Where(x => x.StartDate <= maxDate && x.EndDate >= minDate));
	}

	private async Task<List<Employee>> LoadEmployeesAsync(List<Guid> employeeIds)
	{
		var query = await employeeRepository.GetQueryableAsync();
		return await asyncExecuter.ToListAsync(query.Where(x => employeeIds.Contains(x.Id)));
	}

	private async Task<Dictionary<Guid, List<Group>>> LoadGroupsByYearAsync(List<Guid> academicYearIds)
	{
		var groupsByYear = new Dictionary<Guid, List<Group>>();
		foreach (var academicYearId in academicYearIds)
		{
			groupsByYear[academicYearId] = await groupRepository.GetListWithDetailsAsync(academicYearId);
		}

		return groupsByYear;
	}

	private async Task<List<HrPolicyAssignment>> LoadAssignmentsAsync(
		List<Guid> academicYearIds,
		List<string> targetKeys,
		DateOnly startDate,
		DateOnly endDate)
	{
		var start = startDate.ToDateTime(TimeOnly.MinValue);
		var end = endDate.ToDateTime(TimeOnly.MinValue);
		var query = await assignmentRepository.GetQueryableAsync();
		return await asyncExecuter.ToListAsync(query.Where(x =>
			academicYearIds.Contains(x.AcademicYearId) &&
			x.IsActive &&
			targetKeys.Contains(x.TargetKey) &&
			x.EffectiveFrom <= end &&
			(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= start)));
	}

	private async Task<AttendanceCaptureModePolicyLookup> LoadPolicyModesAsync(List<Guid> academicYearIds, IEnumerable<Guid> assignedPolicyIds)
	{
		var policyIds = assignedPolicyIds.Distinct().ToList();
		var query = await attendancePolicyRepository.GetQueryableAsync();
		var policies = await asyncExecuter.ToListAsync(query.Where(x =>
			policyIds.Contains(x.Id) ||
			(academicYearIds.Contains(x.AcademicYearId) && x.IsDefault)));
		var defaultModes = policies
			.Where(x => x.IsDefault && x.Status == HrPolicyStatus.Active)
			.GroupBy(x => x.AcademicYearId)
			.Where(x => x.Count() == 1)
			.ToDictionary(x => x.Key, x => x.Single().CaptureMode);
		var assignedModes = policies
			.Where(x => x.Status == HrPolicyStatus.Active)
			.ToDictionary(x => x.Id, x => (x.AcademicYearId, x.CaptureMode));

		return new AttendanceCaptureModePolicyLookup(defaultModes, assignedModes);
	}

	private static Guid? ResolveAcademicYearId(DateOnly date, List<AcademicYear> years)
	{
		var target = date.ToDateTime(TimeOnly.MinValue);
		return years
			.Where(x => x.StartDate <= target && x.EndDate >= target)
			.OrderByDescending(x => x.StartDate)
			.FirstOrDefault()
			?.Id;
	}
}

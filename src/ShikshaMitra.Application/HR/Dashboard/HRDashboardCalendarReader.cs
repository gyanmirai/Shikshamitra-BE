using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Dashboard;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Dashboard;

// Reuses the established effective assignment precedence, with bounded batch reads.
public class HRDashboardCalendarReader(
	IRepository<AcademicYear, Guid> yearRepository,
	IRepository<HrPolicyAssignment, Guid> assignmentRepository,
	IRepository<AttendancePolicy, Guid> policyRepository,
	IRepository<WorkCalendarPolicy, Guid> calendarRepository,
	IRepository<WorkCalendarHoliday, Guid> holidayRepository,
	IGroupRepository groupRepository,
	IAsyncQueryableExecuter executer) : ITransientDependency
{
	internal async Task<Dictionary<(Guid EmployeeId, DateOnly Date), DashboardCalendarDay>> ReadAsync(
		List<Employee> employees, List<DateOnly> dates)
	{
		var result = new Dictionary<(Guid, DateOnly), DashboardCalendarDay>();
		if (employees.Count == 0) return result;
		var start = dates.First().ToDateTime(TimeOnly.MinValue);
		var end = dates.Last().ToDateTime(TimeOnly.MinValue);
		var yearQuery = await yearRepository.GetQueryableAsync();
		var years = await executer.ToListAsync(yearQuery.Where(x => x.StartDate <= end && x.EndDate >= start));
		var yearIds = years.Select(x => x.Id).ToList();
		var groups = new Dictionary<Guid, List<Group>>();
		foreach (var id in yearIds)
			groups[id] = await groupRepository.GetListWithDetailsAsync(id);
		var groupKeys = AttendanceCaptureModeReadLookup.BuildGroupKeysByEmployeeYear(employees, groups);
		var targets = AttendanceCaptureModeReadLookup.BuildTargetKeys(employees.Select(x => x.Id).ToList(), groupKeys);
		var assignmentQuery = await assignmentRepository.GetQueryableAsync();
		var assignments = await executer.ToListAsync(assignmentQuery.Where(x =>
			yearIds.Contains(x.AcademicYearId) && x.IsActive && targets.Contains(x.TargetKey) &&
			x.EffectiveFrom <= end && (!x.EffectiveTo.HasValue || x.EffectiveTo >= start)));
		var policyQuery = await policyRepository.GetQueryableAsync();
		var policies = await executer.ToListAsync(policyQuery.Where(x => yearIds.Contains(x.AcademicYearId) && x.Status == HrPolicyStatus.Active));
		var calendarsQuery = await calendarRepository.GetQueryableAsync();
		var calendars = (await executer.ToListAsync(calendarsQuery.Where(x =>
			yearIds.Contains(x.AcademicYearId) && x.Status == HrPolicyStatus.Active))).ToDictionary(x => x.Id);
		var calendarIds = calendars.Keys.ToList();
		var holidayQuery = await holidayRepository.GetQueryableAsync();
		var holidays = await executer.ToListAsync(holidayQuery.Where(x =>
			calendarIds.Contains(x.WorkCalendarPolicyId) && x.IsIncluded && x.FromDate <= dates.Last() && x.ToDate >= dates.First()));
		var holidaysByCalendar = holidays.ToLookup(x => x.WorkCalendarPolicyId);
		var policyById = policies.ToDictionary(x => x.Id);
		var defaults = policies.Where(x => x.IsDefault).GroupBy(x => x.AcademicYearId)
			.Where(x => x.Count() == 1).ToDictionary(x => x.Key, x => x.Single());

		foreach (var date in dates)
		{
			var day = date.ToDateTime(TimeOnly.MinValue);
			var matchingYears = years.Where(x => x.StartDate <= day && x.EndDate >= day).ToList();
			foreach (var employee in employees)
			{
				var key = (employee.Id, date);
				result[key] = new(null, null);
				if (matchingYears.Count != 1) continue;
				var yearId = matchingYears[0].Id;
				var assignment = AttendanceCaptureModeReadLookup.SelectAssignment(employee.Id, yearId, date, assignments, groupKeys);
				var policy = assignment == null ? defaults.GetValueOrDefault(yearId) : policyById.GetValueOrDefault(assignment.AttendancePolicyId);
				if (policy == null || policy.AcademicYearId != yearId ||
					!calendars.TryGetValue(policy.WorkCalendarPolicyId, out var calendar) || calendar.AcademicYearId != yearId ||
					(calendar.EffectiveFrom.HasValue && calendar.EffectiveFrom.Value.Date > day) ||
					(calendar.EffectiveTo.HasValue && calendar.EffectiveTo.Value.Date < day)) continue;
				var holiday = holidaysByCalendar[calendar.Id].Any(x => x.FromDate <= date && x.ToDate >= date);
				var weeklyOff = EmployeeDashboardProjection.IsWeeklyOff(date.DayOfWeek, calendar.WeeklyOffPattern, calendar.GetCustomWeeklyOffs());
				result[key] = new(!holiday && !weeklyOff, policy.CaptureMode);
			}
		}
		return result;
	}
}

internal sealed record DashboardCalendarDay(bool? IsWorkingDay, AttendanceCaptureMode? CaptureMode);

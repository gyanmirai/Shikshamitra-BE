using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Dashboard;

public class HRDashboardOperationalStaffReader(
	HrEmployeeScopeQueryService scopeQuery,
	IEmployeeAttendanceRepository attendanceRepository,
	IEmployeeLeaveRepository leaveRepository,
	IRepository<ShiftManagement, Guid> shiftRepository,
	IRepository<EducationMediumStage, Guid> stageRepository,
	HRDashboardCalendarReader calendarReader,
	IAsyncQueryableExecuter executer) : ITransientDependency
{
	internal async Task<DashboardStaffPopulation> LoadPopulationAsync(
		HRDashboardOverviewInput input, UserScopeContext scope, DateOnly start, DateOnly end)
	{
		var query = await scopeQuery.BuildScopedEmployeeQueryAsync(scope, new HrEmployeeScopeOptions
		{
			ActiveOnOrAfter = start, JoinedOnOrBefore = end, TreatNullStatusAsActive = true,
			EducationMediumStageId = input.EducationMediumStageId
		});
		// Filter choices come from the authorized population, before user-selected filters.
		var all = await executer.ToListAsync(query);
		var stageIdsByEmployee = await scopeQuery.BuildEmployeeStageLookupAsync(all.Select(x => x.Id));
		var allowedStages = HrUserScopeAccess.HasHrWideAccess(scope)
			? stageIdsByEmployee.Values.SelectMany(x => x).Distinct().ToList()
			: await scopeQuery.GetAccessibleStageIdsAsync(scope);
		var displayStageIds = stageIdsByEmployee.Values.SelectMany(x => x).Where(allowedStages.Contains).Distinct().ToList();
		var stageQuery = await stageRepository.GetQueryableAsync();
		var selectedDay = end.ToDateTime(TimeOnly.MinValue);
		var stageRows = await executer.ToListAsync(stageQuery.Where(x => displayStageIds.Contains(x.Id) &&
			x.EducationMedium.EducationBoard.AcademicYear.StartDate <= selectedDay &&
			x.EducationMedium.EducationBoard.AcademicYear.EndDate >= selectedDay)
			.Select(x => new { x.Id, x.Name, Medium = x.EducationMedium.Name, Board = x.EducationMedium.EducationBoard.Type }));
		var stageNames = stageRows.ToDictionary(x => x.Id, x => $"{x.Board} / {x.Medium} / {x.Name}");
		var shiftIds = all.Where(x => x.ShiftId.HasValue).Select(x => x.ShiftId!.Value).Distinct().ToList();
		var shiftQuery = await shiftRepository.GetQueryableAsync();
		var shifts = await executer.ToListAsync(shiftQuery.Where(x => shiftIds.Contains(x.Id)));
		var options = new HRDashboardFilterOptionsDto
		{
			Departments = all.Select(x => x.Department).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).Distinct().OrderBy(x => x).ToList(),
			Stages = stageNames.Select(x => new HRDashboardOptionDto { Id = x.Key, Name = x.Value }).OrderBy(x => x.Name).ToList(),
			Shifts = shifts.Select(x => new HRDashboardOptionDto { Id = x.Id, Name = x.ShiftName }).OrderBy(x => x.Name).ToList()
		};
		var employees = all.Where(x =>
			(!input.IsTeaching.HasValue || (x.EmployeeGroup == EmployeeGroupType.Teaching) == input.IsTeaching.Value) &&
			(string.IsNullOrWhiteSpace(input.Department) || string.Equals(x.Department, input.Department.Trim(), StringComparison.OrdinalIgnoreCase)) &&
			(!input.ShiftId.HasValue || x.ShiftId == input.ShiftId)).ToList();
		return new(employees, options, stageIdsByEmployee, stageNames, shifts.ToDictionary(x => x.Id, x => x.ShiftName));
	}

	internal async Task<Dictionary<DateOnly, List<HRDashboardStaffDto>>> ReadDaysAsync(
		DashboardStaffPopulation population, List<DateOnly> dates)
	{
		var employees = population.Employees;
		var ids = employees.Select(x => x.Id).ToList();
		var start = dates.First();
		var end = dates.Last();
		var attendanceQuery = await attendanceRepository.GetQueryableAsync();
		var attendance = await executer.ToListAsync(attendanceQuery.Where(x => ids.Contains(x.EmployeeId) &&
			x.AttendanceDate >= start && x.AttendanceDate <= end));
		var attendanceLookup = attendance.GroupBy(x => (x.EmployeeId, x.AttendanceDate))
			.ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.LastModificationTime ?? y.CreationTime).ThenBy(y => y.Id).First());
		var conflictingDays = attendance.GroupBy(x => (x.EmployeeId, x.AttendanceDate))
			.Where(x => x.Select(row => row.StatusType).Distinct().Take(2).Count() > 1).Select(x => x.Key).ToHashSet();
		var leaveQuery = await leaveRepository.GetQueryableAsync();
		var leaves = await executer.ToListAsync(leaveQuery.Where(x => ids.Contains(x.EmployeeId) &&
			x.Status == EmployeeLeaveStatusType.Approved && x.StartDateOnly <= end && x.EndDateOnly >= start)
			.Select(x => new { x.EmployeeId, x.StartDateOnly, x.EndDateOnly, x.HalfDay }));
		var leaveLookup = leaves.ToLookup(x => x.EmployeeId);
		var calendars = await calendarReader.ReadAsync(employees, dates);
		var result = new Dictionary<DateOnly, List<HRDashboardStaffDto>>();
		foreach (var date in dates)
		{
			var rows = new List<HRDashboardStaffDto>();
			foreach (var employee in employees.Where(x => IsEligible(x, date)))
			{
				attendanceLookup.TryGetValue((employee.Id, date), out var recorded);
				var calendar = calendars.GetValueOrDefault((employee.Id, date), new(null, null));
				var coveringLeaves = leaveLookup[employee.Id].Where(x => x.StartDateOnly <= date && x.EndDateOnly >= date).ToList();
				var fullLeave = coveringLeaves.Any(x => !x.HalfDay);
				var halfLeave = coveringLeaves.Any(x => x.HalfDay);
				var incomplete = recorded != null && recorded.Source is AttendanceRecordSource.Integration or AttendanceRecordSource.CsvImport &&
					(recorded.TimeIn.HasValue != recorded.TimeOut.HasValue);
				var (status, flags) = HRDashboardDailyStatusProjection.Classify(recorded?.StatusType, fullLeave, halfLeave, calendar.IsWorkingDay, incomplete);
				if (conflictingDays.Contains((employee.Id, date)))
					flags.Add(new(HRDashboardReviewFlagCode.ConflictingAttendanceRecords, "Attendance records disagree for this date."));
				rows.Add(new HRDashboardStaffDto
				{
					EmployeeId = employee.Id,
					Name = string.Join(" ", new[] { employee.FirstName, employee.MiddleName, employee.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))),
					EmployeeCode = employee.EmployeeCode ?? "",
					IsTeaching = employee.EmployeeGroup == EmployeeGroupType.Teaching,
					Department = employee.Department,
					Stages = population.StageIds.GetValueOrDefault(employee.Id, []).Where(population.StageNames.ContainsKey)
						.Select(x => new HRDashboardOptionDto { Id = x, Name = population.StageNames[x] }).ToList(),
					ShiftName = employee.ShiftId.HasValue ? population.ShiftNames.GetValueOrDefault(employee.ShiftId.Value) : null,
					Status = status, ApprovedLeaveCoverage = fullLeave ? HRDashboardLeaveCoverage.FullDay : halfLeave ? HRDashboardLeaveCoverage.HalfDay : null,
					Source = recorded?.Source.ToString(), TimeIn = recorded?.TimeIn, TimeOut = recorded?.TimeOut, ReviewFlags = flags
				});
			}
			result[date] = rows;
		}
		return result;
	}

	internal static bool IsEligible(Employee employee, DateOnly date) =>
		(employee.DateOfJoining ?? DateOnly.FromDateTime(employee.CreationTime)) <= date &&
		EmployeeLifecycleEligibility.IsAttendanceEligibleOnDate(employee.EmployeeStatusType, employee.StatusEffectiveDate, date);
}

internal sealed record DashboardStaffPopulation(
	List<Employee> Employees, HRDashboardFilterOptionsDto Filters,
	Dictionary<Guid, List<Guid>> StageIds, Dictionary<Guid, string> StageNames, Dictionary<Guid, string> ShiftNames);

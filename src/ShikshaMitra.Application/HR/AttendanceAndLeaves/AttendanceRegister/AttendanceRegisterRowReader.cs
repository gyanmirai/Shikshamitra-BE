using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;
using static ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegisterProjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class AttendanceRegisterRowReader(
	IEmployeeAttendanceRepository employeeAttendanceRepository,
	HrEmployeeScopeQueryService hrEmployeeScopeQueryService,
	AttendanceRegisterLookupReader lookupReader,
	AttendanceCaptureModeLookupResolver captureModeLookupResolver,
	AttendanceRegisterRowProjectionBuilder rowProjectionBuilder,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	internal async Task<List<RegisterProjection>> BuildRowsAsync(
		GetAttendanceRegisterInput input,
		DateOnly startDate,
		DateOnly endDate,
		UserScopeContext scope)
	{
		var employees = await BuildFilteredEmployeesAsync(input, startDate, endDate, scope);
		return await BuildEmployeeDrivenRowsAsync(input, startDate, endDate, employees);
	}

	private async Task<List<EmployeeProjection>> BuildFilteredEmployeesAsync(
		GetAttendanceRegisterInput input,
		DateOnly startDate,
		DateOnly endDate,
		UserScopeContext scope)
	{
		var employeeQuery = await hrEmployeeScopeQueryService.BuildScopedEmployeeQueryAsync(
			scope,
			new HrEmployeeScopeOptions
			{
				AcademicYearId = input.AcademicYearId,
				EducationMediumStageId = input.EducationMediumStageId,
				ActiveOnOrAfter = startDate,
				JoinedOnOrBefore = endDate,
				TreatNullStatusAsActive = true
			});

		if (input.EmployeeId.HasValue)
		{
			employeeQuery = employeeQuery.Where(x => x.Id == input.EmployeeId);
		}
		if (input.EmployeeGroup.HasValue)
		{
			employeeQuery = employeeQuery.Where(x => x.EmployeeGroup == input.EmployeeGroup);
		}
		if (input.IsTeaching.HasValue)
		{
			employeeQuery = input.IsTeaching.Value
				? employeeQuery.Where(x => x.EmployeeGroup == EmployeeGroupType.Teaching)
				: employeeQuery.Where(x => x.EmployeeGroup != EmployeeGroupType.Teaching);
		}
		if (!string.IsNullOrWhiteSpace(input.Department))
		{
			var department = input.Department.Trim();
			employeeQuery = employeeQuery.Where(x => x.Department != null && x.Department.Contains(department));
		}
		if (input.RoleId.HasValue)
		{
			employeeQuery = employeeQuery.Where(x => x.RoleId == input.RoleId);
		}

		var employees = await asyncExecuter.ToListAsync(employeeQuery.Select(x => new EmployeeProjection
		{
			EmployeeId = x.Id,
			EmployeeName = ((x.FirstName ?? "") + " " + (x.LastName ?? "")).Trim(),
			EmployeeCode = x.EmployeeCode,
			MobileNo = x.MobileNo,
			Department = x.Department,
			EmployeeGroup = x.EmployeeGroup,
			RoleId = x.RoleId,
			DateOfJoining = x.DateOfJoining,
			CreationTime = x.CreationTime,
			EmployeeStatusType = x.EmployeeStatusType,
			StatusLastChangedAt = x.StatusLastChangedAt,
			StatusEffectiveDate = x.StatusEffectiveDate
		}));

		if (employees.Count == 0)
		{
			return employees;
		}

		var employeeIds = employees.Select(x => x.EmployeeId).ToList();
		var stageLookup = await hrEmployeeScopeQueryService.BuildEmployeeStageLookupAsync(
			employeeIds,
			input.AcademicYearId);

		foreach (var employee in employees)
		{
			var stages = stageLookup.TryGetValue(employee.EmployeeId, out var assignedStages)
				? assignedStages
				: [];
			employee.AssignedStageIds = stages;
			employee.EducationMediumStageId = await hrEmployeeScopeQueryService.ResolveDisplayStageAsync(stages, scope, input.EducationMediumStageId);
		}

		return employees;
	}

	private async Task<List<RegisterProjection>> BuildEmployeeDrivenRowsAsync(
		GetAttendanceRegisterInput input,
		DateOnly startDate,
		DateOnly endDate,
		List<EmployeeProjection> employees)
	{
		if (employees.Count == 0)
		{
			return [];
		}

		var employeeIds = employees.Select(x => x.EmployeeId).ToList();
		var attendanceQuery = await employeeAttendanceRepository.GetQueryableAsync();

		attendanceQuery = attendanceQuery.Where(x =>
			employeeIds.Contains(x.EmployeeId) &&
			x.AttendanceDate >= startDate &&
			x.AttendanceDate <= endDate);

		if (input.EducationMediumStageId.HasValue)
		{
			attendanceQuery = attendanceQuery.Where(x =>
				x.EducationMediumStageId == input.EducationMediumStageId.Value);
		}
		else if (input.AcademicYearId.HasValue)
		{
			var allowedStageIds = employees
				.SelectMany(x => x.AssignedStageIds)
				.Distinct()
				.ToList();
			attendanceQuery = attendanceQuery.Where(x =>
				x.EducationMediumStageId.HasValue &&
				allowedStageIds.Contains(x.EducationMediumStageId.Value));
		}

		var attendanceRows = await asyncExecuter.ToListAsync(attendanceQuery);
		var attendanceByEmployeeDate = attendanceRows
			.GroupBy(x => (x.EmployeeId, x.AttendanceDate))
			.ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.LastModificationTime ?? y.CreationTime).First());
		var holidayLookup = await lookupReader.BuildHolidayLookupAsync(Enumerable.Range(0, endDate.DayNumber - startDate.DayNumber + 1)
			.Select(offset => startDate.AddDays(offset)));
		var dates = Enumerable.Range(0, endDate.DayNumber - startDate.DayNumber + 1)
			.Select(offset => startDate.AddDays(offset))
			.ToList();
		var captureModeLookup = await captureModeLookupResolver.ResolveForDatesAsync(employeeIds, dates);

		return rowProjectionBuilder.Build(
			input,
			startDate,
			endDate,
			employees,
			attendanceByEmployeeDate,
			holidayLookup,
			captureModeLookup);
	}
}

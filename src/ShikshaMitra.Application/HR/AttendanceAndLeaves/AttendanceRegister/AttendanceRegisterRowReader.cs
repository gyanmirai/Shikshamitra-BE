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
		var employees = await LoadFilteredEmployeesAsync(input, startDate, endDate, scope, input.AcademicYearId);
		if (employees.Count == 0 && ShouldRetryExplicitEmployeeWithoutAcademicYear(input))
		{
			employees = await LoadFilteredEmployeesAsync(input, startDate, endDate, scope, academicYearId: null);
		}

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
			if (input.EducationMediumStageId.HasValue)
			{
				stages = stages
					.Where(x => x == input.EducationMediumStageId.Value)
					.ToList();
			}
			employee.AssignedStageIds = stages;
			employee.EducationMediumStageId = await hrEmployeeScopeQueryService.ResolveDisplayStageAsync(stages, scope, input.EducationMediumStageId);
		}

		return employees;
	}

	private async Task<List<EmployeeProjection>> LoadFilteredEmployeesAsync(
		GetAttendanceRegisterInput input,
		DateOnly startDate,
		DateOnly endDate,
		UserScopeContext scope,
		Guid? academicYearId)
	{
		var employeeQuery = await hrEmployeeScopeQueryService.BuildScopedEmployeeQueryAsync(
			scope,
			new HrEmployeeScopeOptions
			{
				AcademicYearId = academicYearId,
				EducationMediumStageId = input.EducationMediumStageId,
				ActiveOnOrAfter = startDate,
				JoinedOnOrBefore = endDate,
				TreatNullStatusAsActive = true,
				IncludeUnassignedEmployeesWhenAcademicYearScoped =
					academicYearId.HasValue && !input.EducationMediumStageId.HasValue
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
		employeeQuery = await ApplySearchPrefilterAsync(employeeQuery, input.Search, startDate, endDate);

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
		return employees;
	}

	private async Task<IQueryable<Employee>> ApplySearchPrefilterAsync(
		IQueryable<Employee> employeeQuery,
		string? search,
		DateOnly startDate,
		DateOnly endDate)
	{
		if (string.IsNullOrWhiteSpace(search))
		{
			return employeeQuery;
		}

		var trimmed = search.Trim().ToLowerInvariant();
		var attendanceQuery = await employeeAttendanceRepository.GetQueryableAsync();
		var remarkEmployeeIds = attendanceQuery
			.Where(x =>
				x.AttendanceDate >= startDate &&
				x.AttendanceDate <= endDate &&
				x.Remarks != null &&
				x.Remarks.ToLower().Contains(trimmed))
			.Select(x => x.EmployeeId);

		return employeeQuery.Where(x =>
			(((x.FirstName ?? "") + " " + (x.LastName ?? "")).ToLower().Contains(trimmed)) ||
			(x.EmployeeCode ?? "").ToLower().Contains(trimmed) ||
			(x.MobileNo ?? "").ToLower().Contains(trimmed) ||
			remarkEmployeeIds.Contains(x.Id));
	}

	private static bool ShouldRetryExplicitEmployeeWithoutAcademicYear(GetAttendanceRegisterInput input) =>
		input.EmployeeId.HasValue &&
		input.AcademicYearId.HasValue &&
		!input.EducationMediumStageId.HasValue;

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

		var attendanceRows = await asyncExecuter.ToListAsync(attendanceQuery);
		var attendanceByEmployeeDate = attendanceRows
			.GroupBy(x => (x.EmployeeId, x.AttendanceDate))
			.ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.LastModificationTime ?? y.CreationTime).First());
		var dates = Enumerable.Range(0, endDate.DayNumber - startDate.DayNumber + 1)
			.Select(offset => startDate.AddDays(offset))
			.ToList();
		var holidayLookup = await lookupReader.BuildEmployeeHolidayLookupAsync(employeeIds, dates);
		var captureModeLookup = await captureModeLookupResolver.ResolveForDatesOrDefaultAsync(employeeIds, dates);

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

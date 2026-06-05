using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Common;
using ShikshaMitra.HR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Employees;

[RemoteService(false)]
public class HrEmployeeScopeQueryService(
	IRepository<Employee, Guid> employeeRepository,
	IRepository<EmployeeOrganizationAssignment, Guid> employeeOrganizationAssignmentRepository,
	IEmployeeAttendanceRepository employeeAttendanceRepository,
	HrUserScopeAccessService hrScopeAccessService,
	HrMediumStageScopeReader stageScopeReader)
	: ApplicationService, ITransientDependency
{
	public async Task<List<Guid>> GetAccessibleStageIdsAsync(UserScopeContext scope)
	{
		return await hrScopeAccessService.ResolveAccessibleStageIdsAsync(scope);
	}

	public async Task<IQueryable<Employee>> BuildScopedEmployeeQueryAsync(
		UserScopeContext scope,
		HrEmployeeScopeOptions? options = null)
	{
		options ??= new HrEmployeeScopeOptions();

		var employeeQuery = await employeeRepository.GetQueryableAsync();
		var assignmentQuery = await employeeOrganizationAssignmentRepository.GetQueryableAsync();

		employeeQuery = ApplyEmployeeLifecycleFilter(employeeQuery, options);
		employeeQuery = await ApplyEmployeeScopeFilterAsync(
			employeeQuery,
			assignmentQuery,
			scope,
			options.EducationMediumStageId,
			options.AcademicYearId);
		return employeeQuery;
	}

	public async Task<Dictionary<Guid, List<Guid>>> BuildEmployeeStageLookupAsync(
		IEnumerable<Guid> employeeIds,
		Guid? academicYearId = null)
	{
		var ids = employeeIds.Distinct().ToList();
		if (ids.Count == 0)
		{
			return new Dictionary<Guid, List<Guid>>();
		}

		var assignmentQuery = await employeeOrganizationAssignmentRepository.GetQueryableAsync();

		var rows = await AsyncExecuter.ToListAsync(
			assignmentQuery
				.Where(x => ids.Contains(x.EmployeeId))
				.Select(x => new { x.EmployeeId, x.EducationMediumStageId }));

		if (academicYearId.HasValue)
		{
			var stageIds = rows.Select(x => x.EducationMediumStageId).Distinct().ToList();
			var stageScopes = await stageScopeReader.GetByStageIdsAsync(stageIds);
			rows = rows
				.Where(x =>
					stageScopes.TryGetValue(x.EducationMediumStageId, out var stageScope) &&
					stageScope.AcademicYearId == academicYearId.Value)
				.ToList();
		}

		return rows
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(
				x => x.Key,
				x => x.Select(y => y.EducationMediumStageId).Distinct().ToList());
	}

	public async Task<IQueryable<EmployeeAttendance>> BuildScopedAttendanceQueryAsync(
		UserScopeContext scope,
		Guid? educationMediumStageId = null)
	{
		var attendanceQuery = await employeeAttendanceRepository.GetQueryableAsync();
		return await ApplyScopedAttendanceFilterAsync(attendanceQuery, scope, educationMediumStageId);
	}

	public async Task<IQueryable<EmployeeAttendance>> ApplyScopedAttendanceFilterAsync(
		IQueryable<EmployeeAttendance> attendanceQuery,
		UserScopeContext scope,
		Guid? educationMediumStageId = null)
	{
		if (educationMediumStageId.HasValue)
		{
			await EnsureStageInScopeAsync(scope, educationMediumStageId.Value);
			return attendanceQuery.Where(x => x.EducationMediumStageId == educationMediumStageId.Value);
		}

		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return attendanceQuery;
		}

		var accessibleStageIds = await hrScopeAccessService.ResolveAccessibleStageIdsAsync(scope);
		return attendanceQuery.Where(x =>
			x.EducationMediumStageId.HasValue &&
			accessibleStageIds.Contains(x.EducationMediumStageId.Value));
	}

	public Guid? ResolveDisplayStage(
		IReadOnlyCollection<Guid> employeeStageIds,
		UserScopeContext scope,
		Guid? educationMediumStageId = null)
	{
		if (educationMediumStageId.HasValue)
		{
			return educationMediumStageId.Value;
		}

		if (employeeStageIds.Count == 0)
		{
			return null;
		}

		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return employeeStageIds.First();
		}

		var scopedStage = employeeStageIds.FirstOrDefault(stageId => scope.CanAccessMediumStage(stageId));
		return scopedStage == Guid.Empty ? null : scopedStage;
	}

	private async Task<IQueryable<Employee>> ApplyEmployeeScopeFilterAsync(
		IQueryable<Employee> employeeQuery,
		IQueryable<EmployeeOrganizationAssignment> assignmentQuery,
		UserScopeContext scope,
		Guid? educationMediumStageId,
		Guid? academicYearId)
	{
		List<Guid>? academicYearStageIds = null;
		if (academicYearId.HasValue)
		{
			academicYearStageIds = (await stageScopeReader.GetAllAsync())
				.Where(x => x.AcademicYearId == academicYearId.Value)
				.Select(x => x.StageId)
				.ToList();
		}

		if (educationMediumStageId.HasValue)
		{
			await EnsureStageInScopeAsync(scope, educationMediumStageId.Value);
			if (academicYearStageIds is not null && !academicYearStageIds.Contains(educationMediumStageId.Value))
			{
				throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
			}
			var stageEmployeeIds = assignmentQuery
				.Where(x => x.EducationMediumStageId == educationMediumStageId.Value)
				.Select(x => x.EmployeeId);
			return employeeQuery.Where(x => stageEmployeeIds.Contains(x.Id));
		}

		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			if (academicYearStageIds is not null)
			{
				var academicYearEmployeeIds = assignmentQuery
					.Where(x => academicYearStageIds.Contains(x.EducationMediumStageId))
					.Select(x => x.EmployeeId);
				return employeeQuery.Where(x => academicYearEmployeeIds.Contains(x.Id));
			}

			return employeeQuery;
		}

		if (!HrUserScopeAccess.HasAnyConcreteHrStageAccess(scope))
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
		}

		var accessibleStageIds = await hrScopeAccessService.ResolveAccessibleStageIdsAsync(scope);
		if (academicYearStageIds is not null)
		{
			accessibleStageIds = accessibleStageIds
				.Where(academicYearStageIds.Contains)
				.ToList();
		}

		var accessibleEmployeeIds = assignmentQuery
			.Where(x => accessibleStageIds.Contains(x.EducationMediumStageId))
			.Select(x => x.EmployeeId);
		return employeeQuery.Where(x => accessibleEmployeeIds.Contains(x.Id));
	}

	private static IQueryable<Employee> ApplyEmployeeLifecycleFilter(
		IQueryable<Employee> query,
		HrEmployeeScopeOptions options)
	{
		if (options.ActiveOnOrAfter.HasValue)
		{
			query = query.Where(x =>
				(!x.EmployeeStatusType.HasValue && options.TreatNullStatusAsActive) ||
				x.EmployeeStatusType == EmployeeStatusType.Active ||
				(x.EmployeeStatusType != EmployeeStatusType.Active &&
				 x.StatusEffectiveDate.HasValue &&
				 x.StatusEffectiveDate.Value >= options.ActiveOnOrAfter.Value));
		}

		if (options.JoinedOnOrBefore.HasValue)
		{
			var joinedEndExclusive = options.JoinedOnOrBefore.Value.ToDateTime(TimeOnly.MinValue).AddDays(1);
			query = query.Where(x =>
				(x.DateOfJoining.HasValue && x.DateOfJoining.Value <= options.JoinedOnOrBefore.Value) ||
				(!x.DateOfJoining.HasValue && x.CreationTime < joinedEndExclusive));
		}

		return query;
	}

	private async Task EnsureStageInScopeAsync(UserScopeContext scope, Guid educationMediumStageId)
	{
		if (await hrScopeAccessService.CanAccessEmployeeStageAsync(scope, educationMediumStageId))
		{
			return;
		}

		throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
	}
}

public class HrEmployeeScopeOptions
{
	public Guid? AcademicYearId { get; set; }
	public Guid? EducationMediumStageId { get; set; }
	public DateOnly? ActiveOnOrAfter { get; set; }
	public DateOnly? JoinedOnOrBefore { get; set; }
	public bool TreatNullStatusAsActive { get; set; } = true;
}

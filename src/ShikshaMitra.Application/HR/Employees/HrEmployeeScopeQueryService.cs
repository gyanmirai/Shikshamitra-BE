using ShikshaMitra.Security.UserScope;
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
	IRepository<EmployeeOrganizationAssignment, Guid> employeeOrganizationAssignmentRepository,
	IEmployeeAttendanceRepository employeeAttendanceRepository,
	HrUserScopeAccessService hrScopeAccessService,
	HrMediumStageScopeReader stageScopeReader,
	HrEmployeeScopedQueryBuilder scopedQueryBuilder)
	: ApplicationService, ITransientDependency
{
	public async Task<List<Guid>> GetAccessibleStageIdsAsync(UserScopeContext scope)
	{
		return await hrScopeAccessService.ResolveAccessibleStageIdsAsync(scope);
	}

	public void EnsureHasConcreteHrAccess(UserScopeContext scope, string operation)
	{
		hrScopeAccessService.EnsureHasConcreteHrAccess(scope, operation);
	}

	public async Task<IQueryable<Employee>> BuildScopedEmployeeQueryAsync(
		UserScopeContext scope,
		HrEmployeeScopeOptions? options = null)
		=> await scopedQueryBuilder.BuildAsync(scope, options);

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

	public async Task<Guid?> ResolveDisplayStageAsync(
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

		var stageScopes = await stageScopeReader.GetByStageIdsAsync(employeeStageIds);
		foreach (var stageId in employeeStageIds)
		{
			stageScopes.TryGetValue(stageId, out var parentScope);
			if (scope.CanAccessMediumStage(
				stageId,
				parentScope?.MediumId,
				parentScope?.BoardId,
				parentScope?.AcademicYearId))
			{
				return stageId;
			}
		}

		return null;
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

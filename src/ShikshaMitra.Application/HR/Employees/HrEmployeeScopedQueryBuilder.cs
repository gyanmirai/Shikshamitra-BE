using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Employees;

public class HrEmployeeScopedQueryBuilder(
	IRepository<Employee, Guid> employeeRepository,
	IRepository<EmployeeOrganizationAssignment, Guid> employeeOrganizationAssignmentRepository,
	HrUserScopeAccessService hrScopeAccessService,
	HrMediumStageScopeReader stageScopeReader)
	: ITransientDependency
{
	public async Task<IQueryable<Employee>> BuildAsync(
		UserScopeContext scope,
		HrEmployeeScopeOptions? options = null)
	{
		options ??= new HrEmployeeScopeOptions();

		var employeeQuery = await employeeRepository.GetQueryableAsync();
		var assignmentQuery = await employeeOrganizationAssignmentRepository.GetQueryableAsync();

		employeeQuery = ApplyLifecycleFilter(employeeQuery, options);
		return await ApplyScopeFilterAsync(
			employeeQuery,
			assignmentQuery,
			scope,
			options);
	}

	private async Task<IQueryable<Employee>> ApplyScopeFilterAsync(
		IQueryable<Employee> employeeQuery,
		IQueryable<EmployeeOrganizationAssignment> assignmentQuery,
		UserScopeContext scope,
		HrEmployeeScopeOptions options)
	{
		List<Guid>? academicYearStageIds = null;
		if (options.AcademicYearId.HasValue)
		{
			academicYearStageIds = (await stageScopeReader.GetAllAsync())
				.Where(x => x.AcademicYearId == options.AcademicYearId.Value)
				.Select(x => x.StageId)
				.ToList();
		}

		if (options.EducationMediumStageId.HasValue)
		{
			await EnsureStageInScopeAsync(scope, options.EducationMediumStageId.Value);
			if (academicYearStageIds is not null && !academicYearStageIds.Contains(options.EducationMediumStageId.Value))
			{
				throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
			}

			var stageEmployeeIds = assignmentQuery
				.Where(x => x.EducationMediumStageId == options.EducationMediumStageId.Value)
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
				if (options.IncludeUnassignedEmployeesWhenAcademicYearScoped)
				{
					return employeeQuery;
				}

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

	private static IQueryable<Employee> ApplyLifecycleFilter(
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

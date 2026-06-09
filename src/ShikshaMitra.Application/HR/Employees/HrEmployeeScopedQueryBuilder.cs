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
			options.EducationMediumStageId,
			options.AcademicYearId);
	}

	private async Task<IQueryable<Employee>> ApplyScopeFilterAsync(
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

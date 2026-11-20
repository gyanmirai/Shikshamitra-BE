using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

[RemoteService(false)]
public class PayrollScopedEmployeeResolver(
	IUserScopeService userScopeService,
	HrEmployeeScopeQueryService hrEmployeeScopeQueryService)
	: ApplicationService, ITransientDependency
{
	internal async Task<List<PayrollTargetEmployee>> ResolveScopedTargetEmployeesAsync(
		int year,
		int month,
		List<Guid>? requestedEmployeeIds)
	{
		var scopedEmployees = (await GetScopedPayrollEmployeesAsync(year, month, includeUnknownStageEmployees: false))
			.Select(e => new PayrollTargetEmployee(e.Id, e.EmployeeCode, e.EmployeeName))
			.ToList();

		return PayrollTargetEmployeeResolver.ResolveTargetEmployees(scopedEmployees, requestedEmployeeIds);
	}

	public async Task<List<Employee>> GetScopedPayrollEmployeesAsync(
		int year,
		int month,
		bool includeUnknownStageEmployees) =>
		await ReadEligibleAsync(await GetScopedEmployeeQueryAsync(includeUnknownStageEmployees), year, month, AsyncExecuter);

	internal static async Task<List<Employee>> ReadEligibleAsync(IQueryable<Employee> employeeQuery, int year, int month, IAsyncQueryableExecuter queries)
	{
		var monthStart = DateOnly.FromDateTime(new DateTime(year, month, 1));
		var monthEnd = PayrollRunRules.GetPayrollMonthEndDate(year, month);
		var joinedEndExclusive = monthEnd.ToDateTime(TimeOnly.MinValue).AddDays(1);
		employeeQuery = employeeQuery.Where(employee =>
			((employee.DateOfJoining.HasValue && employee.DateOfJoining.Value <= monthEnd) ||
			 (!employee.DateOfJoining.HasValue && employee.CreationTime < joinedEndExclusive)) &&
			((!employee.EmployeeStatusType.HasValue) ||
			 employee.EmployeeStatusType == EmployeeStatusType.Active ||
			 (employee.EmployeeStatusType != EmployeeStatusType.Active &&
			  ((employee.StatusEffectiveDate.HasValue &&
			    employee.StatusEffectiveDate.Value >= monthStart) ||
			   (!employee.StatusEffectiveDate.HasValue &&
			    employee.StatusLastChangedAt.HasValue)))));
		var candidates = await queries.ToListAsync(
			employeeQuery
				.OrderBy(e => e.FirstName)
				.ThenBy(e => e.LastName));
		return candidates
			.Where(employee => EmployeeLifecycleEligibility.IsEligibleForPayrollMonth(employee, monthStart, monthEnd))
			.ToList();
	}

	public async Task<List<Employee>> GetScopedEmployeesAsync(bool includeUnknownStageEmployees)
	{
		var employeeQuery = await GetScopedEmployeeQueryAsync(includeUnknownStageEmployees);
		return await AsyncExecuter.ToListAsync(employeeQuery);
	}

	public async Task<List<Employee>> ResolveScopedEmployeesByIdsAsync(
		int year,
		int month,
		IReadOnlyCollection<Guid> requestedEmployeeIds,
		bool includeUnknownStageEmployees)
	{
		var requestedSet = requestedEmployeeIds.Distinct().ToHashSet();
		if (requestedSet.Count == 0)
		{
			return [];
		}

		var scopedEmployees = await GetScopedPayrollEmployeesAsync(year, month, includeUnknownStageEmployees);
		var scopedById = scopedEmployees.ToDictionary(e => e.Id, e => e);
		var outOfScopeIds = requestedSet.Where(id => !scopedById.ContainsKey(id)).ToList();
		if (outOfScopeIds.Count > 0)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope)
				.WithData("employeeIds", outOfScopeIds);
		}

		return requestedSet.Select(id => scopedById[id]).ToList();
	}

	internal async Task<IQueryable<Employee>> GetScopedEmployeeQueryAsync(bool includeUnknownStageEmployees)
	{
		var scope = await EnsureCanOperatePayrollAsync();
		return await BuildEmployeeQueryAsync(scope, includeUnknownStageEmployees);
	}

	internal async Task<IQueryable<Employee>> GetEmployeeQueryForUserAsync(Guid userId)
	{
		var scope = await userScopeService.GetUserScopeAsync(userId);
		if (scope.TenantId != CurrentTenant.Id || !HrUserScopeAccess.HasAnyConcreteHrStageAccess(scope))
			throw new Volo.Abp.Authorization.AbpAuthorizationException();
		return await BuildEmployeeQueryAsync(scope, false);
	}

	private async Task<IQueryable<Employee>> BuildEmployeeQueryAsync(UserScopeContext scope, bool includeUnknownStageEmployees)
	{
		var employeeQuery = await hrEmployeeScopeQueryService.BuildScopedEmployeeQueryAsync(scope);
		if (includeUnknownStageEmployees || HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return employeeQuery;
		}

		var employeeIds = await AsyncExecuter.ToListAsync(employeeQuery.Select(x => x.Id));
		var stageLookup = await hrEmployeeScopeQueryService.BuildEmployeeStageLookupAsync(employeeIds);
		var knownStageEmployeeIds = stageLookup
			.Where(x => x.Value.Count > 0)
			.Select(x => x.Key)
			.ToList();
		return employeeQuery.Where(x => knownStageEmployeeIds.Contains(x.Id));
	}

	private async Task<UserScopeContext> EnsureCanOperatePayrollAsync()
	{
		var uid = CurrentUser.Id;
		if (!uid.HasValue)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);
		}

		var scope = await userScopeService.GetUserScopeAsync(uid.Value);
		if (scope.TenantId != CurrentTenant.Id)
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return scope;
		}

		hrEmployeeScopeQueryService.EnsureHasConcreteHrAccess(scope, nameof(EnsureCanOperatePayrollAsync));

		return scope;
	}
}

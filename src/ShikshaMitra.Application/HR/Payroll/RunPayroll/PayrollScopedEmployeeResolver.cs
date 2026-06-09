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
		bool includeUnknownStageEmployees)
	{
		var monthStart = DateOnly.FromDateTime(new DateTime(year, month, 1));
		var monthEnd = PayrollRunRules.GetPayrollMonthEndDate(year, month);
		var joinedEndExclusive = monthEnd.ToDateTime(TimeOnly.MinValue).AddDays(1);
		var employeeQuery = await GetScopedEmployeeQueryAsync(includeUnknownStageEmployees);
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
		var candidates = await AsyncExecuter.ToListAsync(
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

	private async Task<IQueryable<Employee>> GetScopedEmployeeQueryAsync(bool includeUnknownStageEmployees)
	{
		var scope = await EnsureCanOperatePayrollAsync();
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
		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return scope;
		}

		hrEmployeeScopeQueryService.EnsureHasConcreteHrAccess(scope, nameof(EnsureCanOperatePayrollAsync));

		return scope;
	}
}

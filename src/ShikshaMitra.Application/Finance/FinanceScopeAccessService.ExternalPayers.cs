using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Authorization;

namespace ShikshaMitra.Finance;

public partial class FinanceScopeAccessService
{
	public async Task<IQueryable<Guid>> GetScopedEmployeeIdsQueryAsync()
	{
		if (!CurrentUser.Id.HasValue || !CurrentTenant.Id.HasValue) throw new AbpAuthorizationException();
		await GetActiveIdentityAsync(CurrentUser.Id.Value);
		var scope = await LazyServiceProvider.LazyGetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
		var employees = (await employeeRepository.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id);
		if (scope.TenantId != CurrentTenant.Id || !HrUserScopeAccess.HasAnyConcreteHrStageAccess(scope))
			return employees.Where(_ => false).Select(row => row.Id);
		var scoped = await LazyServiceProvider.LazyGetRequiredService<HrEmployeeScopeQueryService>()
			.BuildScopedEmployeeQueryAsync(scope);
		return scoped.Where(row => row.TenantId == CurrentTenant.Id).Select(row => row.Id);
	}
}

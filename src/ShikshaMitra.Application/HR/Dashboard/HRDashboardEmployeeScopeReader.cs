using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Dashboard;

public class HRDashboardEmployeeScopeReader(
	IEmployeeRepository employeeRepository,
	HrEmployeeScopeQueryService employeeScopeQueryService) : ITransientDependency
{
	public async Task<IQueryable<Employee>> GetScopedEmployeeQueryAsync(UserScopeContext scope)
	{
		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return await employeeRepository.GetQueryableAsync();
		}

		return await employeeScopeQueryService.BuildScopedEmployeeQueryAsync(scope);
	}
}

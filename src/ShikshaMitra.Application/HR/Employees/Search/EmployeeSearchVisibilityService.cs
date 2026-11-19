using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Employees.Search;

[RemoteService(false)]
public class EmployeeSearchVisibilityService(IUserScopeService scopes, HrEmployeeScopeQueryService employees)
    : ApplicationService, ITransientDependency
{
    public async Task<IQueryable<Employee>> ApplyVisibilityScopeAsync(IQueryable<Employee> query)
    {
        if (!CurrentUser.Id.HasValue) return query.Where(_ => false);
        var userId = CurrentUser.Id.Value;
        var scope = await scopes.GetUserScopeAsync(userId);
        // Personal records retain the existing ownership exception; other records use
        // the same current database scope as employee details and report datasets.
        if (!HrUserScopeAccess.HasAnyConcreteHrStageAccess(scope))
            return query.Where(employee => employee.UserId == userId);
        var authorized = await employees.BuildScopedEmployeeQueryAsync(scope);
        return query.Where(employee => authorized.Select(item => item.Id).Contains(employee.Id));
    }
}

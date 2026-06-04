using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp;

namespace ShikshaMitra.HR.Employees.Search;

[RemoteService(false)]
public class EmployeeSearchReader(
	IEmployeeRepository employeeRepository,
	EmployeeOrganizationScopeResolver organizationScopeResolver,
	EmployeeSearchVisibilityService visibilityService)
	: ApplicationService, ITransientDependency
{
	public async Task<EmployeeListResponse> GetFilteredListAsync(GetEmployeeListInput input)
	{
		var queryable = await employeeRepository.GetQueryableAsync();
		queryable = await visibilityService.ApplyVisibilityScopeAsync(queryable);
		queryable = await EmployeeSearchQuery.ApplyFiltersAsync(queryable, input, organizationScopeResolver);

		var totalCount = await AsyncExecuter.CountAsync(queryable);
		var employees = await AsyncExecuter.ToListAsync(queryable
			.OrderBy(e => e.FirstName)
			.ThenBy(e => e.LastName)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount));

		var organizationsByUserId = await organizationScopeResolver.GetOrganizationsByUserIdsAsync(
			employees.Select(x => x.UserId));
		var pageSize = input.MaxResultCount > 0 ? input.MaxResultCount : 10;

		return new EmployeeListResponse
		{
			TotalCount = totalCount,
			CurrentPage = (input.SkipCount / pageSize) + 1,
			PageSize = pageSize,
			TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
			Items = employees
				.Select(employee => EmployeeSearchProjection.ToSummary(employee, organizationsByUserId))
				.ToList()
		};
	}
}

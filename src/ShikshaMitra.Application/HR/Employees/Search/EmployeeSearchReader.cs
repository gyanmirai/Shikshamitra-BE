using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
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
	public async Task<EmployeeOptionsResponse> GetOptionsAsync(GetEmployeeListInput input)
	{
		var queryable = await employeeRepository.GetQueryableAsync();
		queryable = await visibilityService.ApplyVisibilityScopeAsync(queryable);
		queryable = await EmployeeSearchQuery.ApplyFiltersAsync(queryable, input, organizationScopeResolver);
		var pageSize = Math.Clamp(input.MaxResultCount, 1, 50);

		// Scalar projection avoids loading employee graphs and organization enrichment.
		// One extra row determines pagination without a separate count query.
		var rows = await AsyncExecuter.ToListAsync(queryable
			.OrderBy(e => e.FirstName)
			.ThenBy(e => e.LastName)
			.ThenBy(e => e.Id)
			.Skip(Math.Max(0, input.SkipCount))
			.Take(pageSize + 1)
			.Select(e => new { e.Id, e.FirstName, e.LastName, e.EmployeeStatusType }));

		return new EmployeeOptionsResponse
		{
			HasMore = rows.Count > pageSize,
			Items = rows.Take(pageSize).Select(row => new EmployeeOptionDto
			{
				Id = row.Id,
				FullName = string.Join(" ", new[] { row.FirstName, row.LastName }
					.Where(part => !string.IsNullOrWhiteSpace(part))),
				Status = (int)(row.EmployeeStatusType ?? EmployeeStatusType.Active)
			}).ToList()
		};
	}

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

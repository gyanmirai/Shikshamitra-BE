using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;

namespace ShikshaMitra.HR.Employees.Search;

public static class EmployeeSearchQuery
{
	public static async Task<IQueryable<Employee>> ApplyFiltersAsync(
		IQueryable<Employee> queryable,
		GetEmployeeListInput input,
		EmployeeOrganizationScopeResolver organizationScopeResolver)
	{
		if (!string.IsNullOrEmpty(input.Status) &&
			Enum.TryParse<EmployeeStatusType>(input.Status, true, out var statusType))
		{
			queryable = queryable.Where(e => e.EmployeeStatusType == statusType);
		}

		if (input.IsTeaching.HasValue)
		{
			queryable = input.IsTeaching.Value
				? queryable.Where(e => e.EmployeeGroup == EmployeeGroupType.Teaching)
				: queryable.Where(e => e.EmployeeGroup != EmployeeGroupType.Teaching);
		}

		if (input.RoleId.HasValue)
		{
			queryable = queryable.Where(e => e.RoleId == input.RoleId.Value);
		}

		if (input.MediumStageId.HasValue)
		{
			var matchingUserIds = await organizationScopeResolver.GetUserIdsWithAnyStageAsync([input.MediumStageId.Value]);
			queryable = queryable.Where(e => e.UserId.HasValue && matchingUserIds.Contains(e.UserId.Value));
		}

		if (!string.IsNullOrWhiteSpace(input.Designation))
		{
			var designation = input.Designation.Trim().ToLower();
			queryable = queryable.Where(e => e.Designation != null && e.Designation.ToLower().Contains(designation));
		}

		return ApplySearchFilter(queryable, input.Search);
	}

	private static IQueryable<Employee> ApplySearchFilter(IQueryable<Employee> queryable, string? search)
	{
		if (string.IsNullOrWhiteSpace(search))
		{
			return queryable;
		}

		var searchTerm = search.Trim().ToLower();
		return queryable.Where(e =>
			(e.FirstName != null && e.FirstName.ToLower().Contains(searchTerm)) ||
			(e.LastName != null && e.LastName.ToLower().Contains(searchTerm)) ||
			(e.MiddleName != null && e.MiddleName.ToLower().Contains(searchTerm)) ||
			(e.EmployeeCode != null && e.EmployeeCode.ToLower().Contains(searchTerm)) ||
			(e.Email != null && e.Email.ToLower().Contains(searchTerm)) ||
			(e.MobileNo != null && e.MobileNo.Contains(searchTerm)) ||
			(e.Designation != null && e.Designation.ToLower().Contains(searchTerm)));
	}
}

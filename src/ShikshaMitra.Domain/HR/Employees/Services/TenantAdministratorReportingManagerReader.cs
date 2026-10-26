using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Identity;

namespace ShikshaMitra.HR.Employees;

// Eligibility comes from current database membership, never client role claims.
public class TenantAdministratorReportingManagerReader(
	IRepository<IdentityUser, Guid> users,
	IRepository<IdentityRole, Guid> roles,
	IEmployeeRepository employees,
	IOptions<UserScopeOptions> options) : DomainService
{
	public async Task<List<IdentityUser>> GetEligibleAsync()
	{
		if (!CurrentTenant.Id.HasValue) return [];
		var tenantId = CurrentTenant.Id.Value;
		var adminRoles = options.Value.AdminRoles.Select(name => name.ToUpperInvariant()).ToArray();
		var roleQuery = (await roles.GetQueryableAsync()).Where(role => role.TenantId == tenantId &&
			Enumerable.Contains(adminRoles, role.NormalizedName));
		var employeeQuery = await employees.GetQueryableAsync();
		var query = (await users.GetQueryableAsync()).Where(user => user.TenantId == tenantId &&
			user.IsActive &&
			user.Roles.Any(membership => roleQuery.Select(role => role.Id).Contains(membership.RoleId)) &&
			// Linked profiles, including inactive ones, retain employee eligibility rules.
			!employeeQuery.Any(employee => employee.TenantId == tenantId && employee.UserId == user.Id));
		var administrators = await AsyncExecuter.ToListAsync(query.OrderBy(user => user.UserName));
		// SQLite test fixtures cannot translate DateTimeOffset comparisons. Apply this
		// final account-state check only to the tenant-filtered administrator population.
		return administrators.Where(user => !user.LockoutEnabled || !user.LockoutEnd.HasValue || user.LockoutEnd <= Clock.Now).ToList();
	}
}

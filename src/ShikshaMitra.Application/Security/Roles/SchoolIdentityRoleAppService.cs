using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace ShikshaMitra.Security.Roles;

public class SchoolIdentityRoleAppService : IdentityRoleAppService
{
	private readonly IIdentityRoleRepository roles;
	private readonly IRepository<IdentityUser, Guid> users;
	private readonly IRepository<Employee, Guid> employees;
	private readonly IRoleDeletionLock deletionLock;
	private readonly IDataFilter dataFilter;

	public SchoolIdentityRoleAppService(
		IdentityRoleManager roleManager,
		IIdentityRoleRepository roles,
		IRepository<IdentityUser, Guid> users,
		IRepository<Employee, Guid> employees,
		IRoleDeletionLock deletionLock,
		IDataFilter dataFilter
	) : base(roleManager, roles)
	{
		this.roles = roles;
		this.users = users;
		this.employees = employees;
		this.deletionLock = deletionLock;
		this.dataFilter = dataFilter;
	}

	[Authorize(IdentityPermissions.Roles.Delete)]
	[UnitOfWork(isTransactional: true)]
	public override async Task DeleteAsync(Guid id)
	{
		await CheckPolicyAsync(IdentityPermissions.Roles.Delete);
		var role = await roles.GetAsync(id);
		await deletionLock.AcquireAsync(role.Id);
		using (dataFilter.Disable<ISoftDelete>())
		{
			var employeeQuery = (await employees.GetQueryableAsync())
				.Where(employee => employee.TenantId == CurrentTenant.Id && employee.RoleId == role.Id);
			var userIds = (await users.GetQueryableAsync())
				.Where(user => user.TenantId == CurrentTenant.Id && user.Roles.Any(link => link.RoleId == role.Id))
				.Select(user => user.Id)
				.Union(employeeQuery.Where(employee => employee.UserId.HasValue).Select(employee => employee.UserId!.Value));
			var userCount = await AsyncExecuter.CountAsync(userIds);
			if (userCount > 0)
			{
				throw new BusinessException(RoleErrorCodes.AssignedToUsers)
					.WithData("RoleId", role.Id).WithData("UserCount", userCount);
			}

			var employeeCount = await AsyncExecuter.CountAsync(employeeQuery);
			if (employeeCount > 0)
			{
				throw new BusinessException(RoleErrorCodes.ReferencedByEmployees)
					.WithData("RoleId", role.Id).WithData("EmployeeCount", employeeCount);
			}
		}

		await base.DeleteAsync(id);
	}
}

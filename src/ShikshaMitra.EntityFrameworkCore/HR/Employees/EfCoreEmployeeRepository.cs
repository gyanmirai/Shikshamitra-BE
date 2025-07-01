using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Employees;

public class EfCoreEmployeeRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, Employee, Guid>(dbContextProvider), IEmployeeRepository
{
	public async Task<Employee?> GetEmployeeByUserIdAsync(Guid userId)
	{
		DbSet<Employee> employees = await GetDbSetAsync();

		return await employees
			.FirstOrDefaultAsync(e => e.UserId == userId);
	}

	public async Task<Employee?> GetLastEmployeeAsync()
	{
		DbSet<Employee?> employees = await GetDbSetAsync();

		return await employees
			.Where(e => e.TenantId == CurrentTenant.Id)
			.OrderByDescending(e => Convert.ToInt32(e.EmployeeCode.Substring(3)))
			.FirstOrDefaultAsync();
	}
}
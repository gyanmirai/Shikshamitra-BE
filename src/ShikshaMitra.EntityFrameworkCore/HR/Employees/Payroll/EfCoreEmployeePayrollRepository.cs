using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.HR.Employees.Payroll;

public class EfCoreEmployeePayrollDetailsRepository 
	: EfCoreRepository<ShikshaMitraDbContext, EmployeePayrollDetails, Guid>, 
	  IEmployeePayrollDetailsRepository
{
	public EfCoreEmployeePayrollDetailsRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider) 
		: base(dbContextProvider)
	{
	}

	public async Task<EmployeePayrollDetails?> GetByEmployeeIdAsync(
		Guid employeeId, 
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.FirstOrDefaultAsync(x => x.EmployeeId == employeeId, cancellationToken);
	}
}

public class EfCoreEmployeePayrollComponentRepository 
	: EfCoreRepository<ShikshaMitraDbContext, EmployeePayrollComponent, Guid>, 
	  IEmployeePayrollComponentRepository
{
	public EfCoreEmployeePayrollComponentRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider) 
		: base(dbContextProvider)
	{
	}

	public async Task<List<EmployeePayrollComponent>> GetByEmployeeIdAsync(
		Guid employeeId, 
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.Where(x => x.EmployeeId == employeeId)
			.ToListAsync(cancellationToken);
	}

	public async Task<List<EmployeePayrollComponent>> GetEarningsByEmployeeIdAsync(
		Guid employeeId, 
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.Where(x => x.EmployeeId == employeeId && x.IsEarning)
			.ToListAsync(cancellationToken);
	}

	public async Task<List<EmployeePayrollComponent>> GetDeductionsByEmployeeIdAsync(
		Guid employeeId, 
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.Where(x => x.EmployeeId == employeeId && !x.IsEarning)
			.ToListAsync(cancellationToken);
	}

	public async Task DeleteByEmployeeIdAsync(
		Guid employeeId, 
		bool isEarning, 
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		var components = await dbSet
			.Where(x => x.EmployeeId == employeeId && x.IsEarning == isEarning)
			.ToListAsync(cancellationToken);

		foreach (var component in components)
		{
			await DeleteAsync(component, cancellationToken: cancellationToken);
		}
	}
}

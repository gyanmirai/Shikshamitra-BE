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

	public async Task<Dictionary<Guid, EmployeePayrollDetails>> GetByEmployeeIdsAsync(
		IEnumerable<Guid> employeeIds,
		CancellationToken cancellationToken = default)
	{
		var idList = employeeIds.ToList();
		if (idList.Count == 0)
			return new Dictionary<Guid, EmployeePayrollDetails>();

		var dbContext = await GetDbContextAsync();

		// Payroll detail rows are not tenant-scoped; preserve schema by isolating through the parent employee.
		var query = from payrollDetail in dbContext.EmployeePayrollDetails
					join employee in dbContext.Set<Employee>() on payrollDetail.EmployeeId equals employee.Id
					where idList.Contains(payrollDetail.EmployeeId)
					select new { PayrollDetail = payrollDetail, EmployeeTenantId = employee.TenantId };

		var currentTenantId = CurrentTenant.Id;
		if (currentTenantId.HasValue)
		{
			query = query.Where(x => x.EmployeeTenantId == currentTenantId);
		}

		var results = await query.AsNoTracking().ToListAsync(cancellationToken);

		return results
			.Select(x => x.PayrollDetail)
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(g => g.Key, g => g.First());
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

	public async Task<List<EmployeePayrollComponent>> GetByComponentIdAsync(
		Guid componentId,
		bool isEarning,
		CancellationToken cancellationToken = default)
	{
		var dbContext = await GetDbContextAsync();
		// Payroll component rows are not tenant-scoped; preserve schema by isolating through the parent employee.
		var query = from component in dbContext.EmployeePayrollComponents
					join employee in dbContext.Set<Employee>() on component.EmployeeId equals employee.Id
					where component.ComponentId == componentId && component.IsEarning == isEarning
					select new { Component = component, EmployeeTenantId = employee.TenantId };

		var currentTenantId = CurrentTenant.Id;
		if (currentTenantId.HasValue)
		{
			query = query.Where(x => x.EmployeeTenantId == currentTenantId);
		}

		var rows = await query.ToListAsync(cancellationToken);

		return rows.Select(x => x.Component).ToList();
	}

	public async Task DeleteByComponentIdAsync(
		Guid componentId,
		bool isEarning,
		CancellationToken cancellationToken = default)
	{
		var dbContext = await GetDbContextAsync();
		// Payroll component rows are not tenant-scoped; preserve schema by isolating through the parent employee.
		var query = from component in dbContext.EmployeePayrollComponents
					join employee in dbContext.Set<Employee>() on component.EmployeeId equals employee.Id
					where component.ComponentId == componentId && component.IsEarning == isEarning
					select new { Component = component, EmployeeTenantId = employee.TenantId };

		var currentTenantId = CurrentTenant.Id;
		if (currentTenantId.HasValue)
		{
			query = query.Where(x => x.EmployeeTenantId == currentTenantId);
		}

		var components = await query.Select(x => x.Component).ToListAsync(cancellationToken);
		dbContext.EmployeePayrollComponents.RemoveRange(components);
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

	public async Task<ILookup<Guid, EmployeePayrollComponent>> GetByEmployeeIdsAsync(
		IEnumerable<Guid> employeeIds,
		CancellationToken cancellationToken = default)
	{
		var idList = employeeIds.ToList();
		if (idList.Count == 0)
			return Enumerable.Empty<EmployeePayrollComponent>().ToLookup(x => x.EmployeeId);

		var dbContext = await GetDbContextAsync();

		// Payroll component rows are not tenant-scoped; preserve schema by isolating through the parent employee.
		var query = from component in dbContext.EmployeePayrollComponents
					join employee in dbContext.Set<Employee>() on component.EmployeeId equals employee.Id
					where idList.Contains(component.EmployeeId)
					select new { Component = component, EmployeeTenantId = employee.TenantId };

		var currentTenantId = CurrentTenant.Id;
		if (currentTenantId.HasValue)
		{
			query = query.Where(x => x.EmployeeTenantId == currentTenantId);
		}

		var results = await query.AsNoTracking().ToListAsync(cancellationToken);

		return results.Select(x => x.Component).ToLookup(x => x.EmployeeId);
	}
}

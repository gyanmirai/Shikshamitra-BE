using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Employees;

public class EfCoreEmployeeRepository(
	IDbContextProvider<ShikshaMitraDbContext> dbContextProvider,
	IEmployeeCacheManager cacheManager)
	: EfCoreRepository<ShikshaMitraDbContext, Employee, Guid>(dbContextProvider), IEmployeeRepository
{
	public async Task<Employee?> GetEmployeeByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
	{
		var cacheItem = await cacheManager.GetByUserIdOrAddAsync(
			userId,
			async () =>
			{
				var dbSet = await GetDbSetAsync();
				return await dbSet.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken);
			},
			cancellationToken);

		if (cacheItem == null)
		{
			return null;
		}

		var dbSet = await GetDbSetAsync();
		return await dbSet.FirstOrDefaultAsync(e => e.Id == cacheItem.Id, cancellationToken);
	}

	public async Task<Employee?> GetLastEmployeeAsync(CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.OrderByDescending(e => e.CreationTime)
			.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<Employee?> GetWithDetailsAsync(
		Guid id,
		CancellationToken cancellationToken = default)
	{
		var cacheItem = await cacheManager.GetOrAddAsync(
			id,
			async () =>
			{
				var dbSet = await GetDbSetAsync();
				return await dbSet.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
			},
			cancellationToken);

		if (cacheItem == null)
		{
			return null;
		}

		var trackedSet = await GetDbSetAsync();
		return await trackedSet
			.Include(e => e.Documents)
			.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
	}

	public override async Task<Employee> GetAsync(
		Guid id,
		bool includeDetails = true,
		CancellationToken cancellationToken = default)
	{
		if (!includeDetails)
		{
			return await base.GetAsync(id, includeDetails: false, cancellationToken);
		}

		return await GetWithDetailsAsync(id, cancellationToken) ??
			throw new EntityNotFoundException(typeof(Employee), id);
	}

	public override async Task<Employee> UpdateAsync(
		Employee entity,
		bool autoSave = false,
		CancellationToken cancellationToken = default)
	{
		await InvalidateCacheAsync(entity, cancellationToken);
		return await base.UpdateAsync(entity, autoSave, cancellationToken);
	}

	public override async Task DeleteAsync(
		Guid id,
		bool autoSave = false,
		CancellationToken cancellationToken = default)
	{
		var employee = await base.GetAsync(id, includeDetails: false, cancellationToken);
		await InvalidateCacheAsync(employee, cancellationToken);
		await base.DeleteAsync(employee, autoSave, cancellationToken);
	}

	public override async Task<IQueryable<Employee>> WithDetailsAsync()
	{
		return (await GetQueryableAsync())
			.Include(e => e.Documents)
			.AsNoTracking();
	}

	public async Task<Employee?> GetWithReportingManagerAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.Include(e => e.ReportingManager)
			.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
	}

	public async Task<Employee?> GetWithFamilyAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.Include(e => e.FamilyMembers)
			.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
	}

	public async Task<List<Employee>> GetEmployeesByManagerAsync(
		Guid managerId,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.Where(e => e.ReportingManagerId == managerId)
			.OrderBy(e => e.EmployeeCode)
			.ToListAsync(cancellationToken);
	}

	public async Task<Dictionary<string, Employee>> GetEmployeesByCodesAsync(
		IEnumerable<string> employeeCodes,
		CancellationToken cancellationToken = default)
	{
		if (employeeCodes?.Any() != true)
		{
			return new Dictionary<string, Employee>(StringComparer.OrdinalIgnoreCase);
		}

		var normalizedCodes = employeeCodes
			.Where(code => !string.IsNullOrWhiteSpace(code))
			.Select(code => code.Trim().ToUpperInvariant())
			.Distinct()
			.ToList();

		if (normalizedCodes.Count == 0)
		{
			return new Dictionary<string, Employee>(StringComparer.OrdinalIgnoreCase);
		}

		var dbSet = await GetDbSetAsync();
		var employees = await dbSet
			.Where(e => normalizedCodes.Contains(e.EmployeeCode.ToUpper()))
			.ToListAsync(cancellationToken);

		return employees
			.Where(e => !string.IsNullOrWhiteSpace(e.EmployeeCode))
			.ToDictionary(
				e => e.EmployeeCode.ToUpperInvariant(),
				e => e,
				StringComparer.OrdinalIgnoreCase);
	}

	private async Task InvalidateCacheAsync(Employee employee, CancellationToken cancellationToken)
	{
		await cacheManager.InvalidateEmployeeAsync(employee.Id, cancellationToken);

		if (employee.UserId.HasValue)
		{
			await cacheManager.InvalidateByUserIdAsync(employee.UserId.Value, cancellationToken);
		}

		if (!string.IsNullOrEmpty(employee.EmployeeCode))
		{
			await cacheManager.InvalidateByCodeAsync(employee.EmployeeCode, cancellationToken);
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

/// <summary>
/// EF Core repository implementation for SalarySlip entity
/// Optimized for performance with proper indexing support
/// </summary>
public class EfCoreSalarySlipRepository
	: EfCoreRepository<ShikshaMitraDbContext, SalarySlip, Guid>,
	  ISalarySlipRepository
{
	private readonly ICurrentTenant _currentTenant;

	public EfCoreSalarySlipRepository(
		IDbContextProvider<ShikshaMitraDbContext> dbContextProvider,
		ICurrentTenant currentTenant)
		: base(dbContextProvider)
	{
		_currentTenant = currentTenant;
	}

	/// <summary>
	/// Gets a salary slip by employee ID and month-year
	/// Note: Entity is tracked since it may be updated later
	/// </summary>
	public async Task<SalarySlip?> GetByEmployeeAndMonthAsync(
		Guid employeeId,
		string monthYear,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();

		return await dbSet
			.Where(x => x.TenantId == _currentTenant.Id &&
			            x.EmployeeId == employeeId &&
			            x.MonthYear == monthYear)
			.FirstOrDefaultAsync(cancellationToken);
	}

	/// <summary>
	/// Checks if any finalized salary slip exists for a specific month
	/// </summary>
	public async Task<bool> HasFinalizedSlipsForMonthAsync(
		string monthYear,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();

		return await dbSet
			.AsNoTracking()
			.Where(x => x.TenantId == _currentTenant.Id &&
			            x.MonthYear == monthYear &&
			            x.Status == SalarySlipStatus.Finalized)
			.AnyAsync(cancellationToken);
	}

	/// <summary>
	/// Gets all salary slips for a specific month
	/// </summary>
	public async Task<List<SalarySlip>> GetByMonthAsync(
		string monthYear,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();

		return await dbSet
			.Where(x => x.TenantId == _currentTenant.Id &&
			            x.MonthYear == monthYear)
			.ToListAsync(cancellationToken);
	}
}

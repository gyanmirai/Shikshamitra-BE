using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

/// <summary>
/// EF Core repository implementation for SalarySlip entity
/// Optimized for performance with proper indexing support
/// </summary>
public class EfCoreSalarySlipRepository 
	: EfCoreRepository<ShikshaMitraDbContext, SalarySlip, Guid>, 
	  ISalarySlipRepository
{
	public EfCoreSalarySlipRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider) 
		: base(dbContextProvider)
	{
	}

	/// <summary>
	/// Gets a salary slip by employee ID and month-year with optimized query
	/// </summary>
	public async Task<SalarySlip?> GetByEmployeeAndMonthAsync(
		Guid employeeId, 
		string monthYear,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		
		return await dbSet
			.AsNoTracking() // Performance optimization for read-only queries
			.FirstOrDefaultAsync(
				x => x.EmployeeId == employeeId && x.MonthYear == monthYear, 
				cancellationToken);
	}

	/// <summary>
	/// Checks if a salary slip exists for an employee in a specific month
	/// Optimized query that doesn't load the entire entity
	/// </summary>
	public async Task<bool> ExistsAsync(
		Guid employeeId, 
		string monthYear,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		
		return await dbSet
			.AsNoTracking()
			.AnyAsync(
				x => x.EmployeeId == employeeId && x.MonthYear == monthYear, 
				cancellationToken);
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public class EfCoreSalarySlipRepository
	: EfCoreRepository<ShikshaMitraDbContext, SalarySlip, Guid>,
	  ISalarySlipRepository
{
	public EfCoreSalarySlipRepository(
		IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
		: base(dbContextProvider)
	{
	}

	public async Task<SalarySlip?> GetByEmployeeAndMonthAsync(
		Guid employeeId,
		string monthYear,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();

		return await dbSet
			.Where(x => x.EmployeeId == employeeId &&
			            x.MonthYear == monthYear)
			.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<bool> HasFinalizedSlipsForMonthAsync(
		string monthYear,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();

		return await dbSet
			.AsNoTracking()
			.Where(x => x.MonthYear == monthYear &&
			            x.Status == SalarySlipStatus.Finalized)
			.AnyAsync(cancellationToken);
	}

	public async Task<List<SalarySlip>> GetByMonthAsync(
		string monthYear,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();

		return await dbSet
			.Where(x => x.MonthYear == monthYear)
			.ToListAsync(cancellationToken);
	}

	public async Task<(int FinalizedSlipCount, int DraftSlipCount)> GetStatusCountsByEmployeeIdsAsync(
		IReadOnlyCollection<Guid> employeeIds,
		CancellationToken cancellationToken = default)
	{
		if (employeeIds.Count == 0)
		{
			return (0, 0);
		}

		var dbSet = await GetDbSetAsync();
		var statusCounts = await dbSet
			.AsNoTracking()
			.Where(x => employeeIds.Contains(x.EmployeeId))
			.GroupBy(x => x.Status)
			.Select(x => new { Status = x.Key, Count = x.Count() })
			.ToListAsync(cancellationToken);

		return (
			statusCounts.FirstOrDefault(x => x.Status == SalarySlipStatus.Finalized)?.Count ?? 0,
			statusCounts.FirstOrDefault(x => x.Status == SalarySlipStatus.Draft)?.Count ?? 0);
	}
}

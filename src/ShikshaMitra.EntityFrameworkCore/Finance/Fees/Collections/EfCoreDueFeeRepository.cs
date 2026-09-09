using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.FeeCollection;
using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

public class EfCoreDueFeeRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, DueFee, Guid>(dbContextProvider),
	  IDueFeeRepository
{
	public async Task<bool> ExistsForStudentAsync(Guid studentId, CancellationToken ct = default)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet.AnyAsync(x => x.StudentId == studentId, ct);
	}

	public async Task<bool> ExistsForStudentInPeriodAsync(
		Guid studentId, DateTime periodStart, DateTime periodEnd, CancellationToken ct = default)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet.AnyAsync(x => x.StudentId == studentId &&
			((x.CoverageStartDate.HasValue && x.CoverageEndDate.HasValue &&
				x.CoverageStartDate.Value <= periodEnd && x.CoverageEndDate.Value >= periodStart) ||
			 (!x.CoverageStartDate.HasValue && x.Year >= periodStart.Year && x.Year <= periodEnd.Year)), ct);
	}
}

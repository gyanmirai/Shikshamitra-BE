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
}

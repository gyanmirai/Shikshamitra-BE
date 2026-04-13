using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// EF Core repository implementation for EarningComponent
/// </summary>
public class EfCoreEarningComponentRepository : EfCoreRepository<ShikshaMitraDbContext, EarningComponent, Guid>, IEarningComponentRepository
{
	public EfCoreEarningComponentRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
		: base(dbContextProvider)
	{
	}

	public async Task<bool> IsNameExistsAsync(string name, Guid? excludeId = null)
	{
		// Must match aggregate SetName trimming; otherwise availability check can miss a DB collision.
		var normalized = (name ?? string.Empty).Trim();
		var dbSet = await GetDbSetAsync();
		var query = dbSet.Where(x => x.Name.ToLower() == normalized.ToLower());

		if (excludeId.HasValue)
		{
			query = query.Where(x => x.Id != excludeId.Value);
		}

		return await query.AnyAsync();
	}

	public async Task<EarningComponent?> FindByNameAsync(string name)
	{
		var normalized = (name ?? string.Empty).Trim();
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.Where(x => x.Name.ToLower() == normalized.ToLower())
			.FirstOrDefaultAsync();
	}
}

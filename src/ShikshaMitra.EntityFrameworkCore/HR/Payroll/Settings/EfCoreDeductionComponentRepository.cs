using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// EF Core repository implementation for DeductionComponent
/// </summary>
public class EfCoreDeductionComponentRepository : EfCoreRepository<ShikshaMitraDbContext, DeductionComponent, Guid>, IDeductionComponentRepository
{
	public EfCoreDeductionComponentRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
		: base(dbContextProvider)
	{
	}

	public async Task<bool> IsNameExistsAsync(string name, Guid? excludeId = null)
	{
		var dbSet = await GetDbSetAsync();
		var query = dbSet.Where(x => x.Name.ToLower() == name.ToLower());
		
		if (excludeId.HasValue)
		{
			query = query.Where(x => x.Id != excludeId.Value);
		}
		
		return await query.AnyAsync();
	}

	public async Task<DeductionComponent?> FindByNameAsync(string name)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.Where(x => x.Name.ToLower() == name.ToLower())
			.FirstOrDefaultAsync();
	}
}

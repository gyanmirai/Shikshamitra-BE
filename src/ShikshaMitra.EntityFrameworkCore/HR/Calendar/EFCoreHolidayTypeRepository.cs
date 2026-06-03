using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.HR.Calendar.HolidayTypes;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore;

public class EFCoreHolidayTypeRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, HolidayType, Guid>(dbContextProvider), IHolidayTypeRepository
{
	public async Task<HolidayType?> GetByNameAsync(string name)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet.FirstOrDefaultAsync(x => x.Name == name);
	}

	public async Task<bool> ExistsByNameAsync(string name)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet.AnyAsync(x => x.Name == name);
	}
}

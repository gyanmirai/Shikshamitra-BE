using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.HR.Payroll.Settings;

public class EfCoreProvidentFundRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, ProvidentFund, Guid>(dbContextProvider), IProvidentFundRepository
{
	public async Task<bool> IsCodeExistsAsync(string code, Guid? excludeId = null)
	{
		var dbSet = await GetDbSetAsync();
		var query = dbSet.Where(x => x.Code.ToLower() == code.ToLower());

		if (excludeId.HasValue)
		{
			query = query.Where(x => x.Id != excludeId.Value);
		}

		return await query.AnyAsync();
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
}

using Microsoft.EntityFrameworkCore;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.Divisions;
using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.Configurations.Divisions;

public class EfCoreDivisionRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, Division, Guid>(dbContextProvider), IDivisionRepository
{
	public async Task<Division?> FindByNameAndStandardIdAsync(string name, Guid standardId)
	{
		DbSet<Division> dbSet = await GetDbSetAsync();
		return await dbSet.FirstOrDefaultAsync(division => division.Name == name && division.StandardId == standardId);
	}
}
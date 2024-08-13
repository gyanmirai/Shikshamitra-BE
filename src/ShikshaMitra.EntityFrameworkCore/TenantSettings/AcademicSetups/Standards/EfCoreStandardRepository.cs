using Microsoft.EntityFrameworkCore;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.Configurations.Standards;

public class EfCoreStandardRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, Standard, Guid>(dbContextProvider), IStandardRepository
{
	public async Task<Standard?> FindByNameAndEducationMediumStageIdAsync(string name, Guid educationMediumStageId)
	{
		DbSet<Standard> dbSet = await GetDbSetAsync();
		return await dbSet.FirstOrDefaultAsync(standard =>
			standard.Name == name && standard.EducationMediumStageId == educationMediumStageId);
	}
}
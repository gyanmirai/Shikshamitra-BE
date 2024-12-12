using Microsoft.EntityFrameworkCore;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
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
			EF.Functions.Like(standard.Name.ToLower(), name.ToLower()) &&
			standard.EducationMediumStageId == educationMediumStageId
		);
	}

	public async Task<List<Standard>> GetStandardsByEducationMediumStageIdAsync(Guid educationMediumStageId)
	{
		DbSet<Standard> dbSet = await GetDbSetAsync();

		return await dbSet
			.Where(standard => standard.EducationMediumStageId == educationMediumStageId)
			.ToListAsync();
	}

	public async Task<List<Standard>> GetStandardsByEducationMediumIdAsync(Guid educationMediumStageId, int skipCount,
		int maxResultCount)
	{
		DbSet<Standard> dbSet = await GetDbSetAsync();
		return await dbSet.Where(standard => standard.EducationMediumStageId == educationMediumStageId)
			.Skip(skipCount)
			.Take(maxResultCount)
			.ToListAsync();
	}
}
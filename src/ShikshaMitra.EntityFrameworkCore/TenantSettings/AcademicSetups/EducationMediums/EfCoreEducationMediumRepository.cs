using Microsoft.EntityFrameworkCore;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.EducationMediums;
using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.Configurations.EducationalMediums;

public class EfCoreEducationMediumRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, EducationMedium, Guid>(dbContextProvider), IEducationMediumRepository
{
	public async Task<EducationMedium?> FindByNameAndEducationBoardIdAsync(string name, Guid educationBoardId)
	{
		DbSet<EducationMedium> dbSet = await GetDbSetAsync();
		return await dbSet.FirstOrDefaultAsync(educationMedium =>
			educationMedium.Name == name && educationMedium.EducationBoardId == educationBoardId);
	}
}
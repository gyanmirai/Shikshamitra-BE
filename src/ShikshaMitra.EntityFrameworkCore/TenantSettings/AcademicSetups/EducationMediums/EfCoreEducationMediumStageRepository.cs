using Microsoft.EntityFrameworkCore;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.Configurations.EducationalMediums;

public class EfCoreEducationMediumStageRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, EducationMediumStage, Guid>(dbContextProvider),
		IEducationMediumStageRepository
{
	public async Task<EducationMediumStage?> FindByNameAndEducationMediumIdAsync(string name, Guid educationMediumId)
	{
		DbSet<EducationMediumStage> dbSet = await GetDbSetAsync();

		return await dbSet.FirstOrDefaultAsync(educationalMediumStage =>
			EF.Functions.Like(educationalMediumStage.Name.ToLower(), name.ToLower()) && educationalMediumStage.EducationMediumId == educationMediumId
		);
	}
}
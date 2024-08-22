using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.TenantSettings.AcademicSetups;

public class EfCoreEducationBoardRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, EducationBoard, Guid>(dbContextProvider),
		IEducationBoardRepository
{
	public async Task<EducationBoard?> FindByNameAsync(EducationBoardType educationBoardType)
	{
		DbSet<EducationBoard> dbSet = await GetDbSetAsync();
		return await dbSet.FirstOrDefaultAsync(academicYear => academicYear.Type == educationBoardType);
	}

	public async Task<EducationBoard?> FindByNameAndAcademicYearIdAsync(EducationBoardType educationBoardType, Guid academicYearId)
	{
		DbSet<EducationBoard> dbSet = await GetDbSetAsync();
		return await dbSet.FirstOrDefaultAsync(academicYear => academicYear.Type == educationBoardType && academicYear.AcademicYearId == academicYearId);
	}
}
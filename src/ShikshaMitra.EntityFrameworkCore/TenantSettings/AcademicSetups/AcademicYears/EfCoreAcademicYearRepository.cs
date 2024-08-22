using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.TenantSettings.AcademicYears;
using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.TenantSettings.AcademicSetups;

public class EfCoreAcademicYearRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, AcademicYear, Guid>(dbContextProvider),
		IAcademicYearRepository
{
	public async Task<AcademicYear?> FindByNameAsync(string name)
	{
		DbSet<AcademicYear> dbSet = await GetDbSetAsync();
		return await dbSet.FirstOrDefaultAsync(academicYear =>
			EF.Functions.Like(academicYear.Name.ToLower(), name.ToLower()));
	}
}
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Students.StudentRecords;

public class EfCoreStudentRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, Student, Guid>(dbContextProvider), IStudentRepository
{
	public async Task<Student?> GetLastStudentbyGrNoForGSEBAsync(Guid educationBoardId, Guid educationMediumId, Guid educationMediumStageId)
	{
		DbSet<Student> dbSet = await GetDbSetAsync();

		return await dbSet
			.Where(s => s.EducationBoardId == educationBoardId &&
						s.EducationMediumId == educationMediumId &&
						s.EducationMediumStageId == educationMediumStageId)
			.OrderByDescending(s => s.GrNoRecord)
			.FirstOrDefaultAsync();
	}

	public async Task<Student?> GetLastStudentbyGrNoForCBSEAsync(Guid educationBoardId)
	{
		DbSet<Student> dbSet = await GetDbSetAsync();
		return await dbSet
			.Where(s => s.EducationBoardId == educationBoardId)
			.OrderByDescending(s => s.GrNoRecord)
			.FirstOrDefaultAsync();
	}

	public async Task<Student?> GetLastStudentbyRollNoAsync(Guid divisionId)
	{
		DbSet<Student> dbSet = await GetDbSetAsync();
		return await dbSet
			.Where(s => s.DivisionId == divisionId)
			.OrderByDescending(s => s.RollNo)
			.FirstOrDefaultAsync();
	}
}

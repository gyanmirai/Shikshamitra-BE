using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Students;

public class EfCoreStudentRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, Student, Guid>(dbContextProvider), IStudentRepository
{
	public async Task<Student?> GetStudentWithDetailsAsync(Guid studentId)
	{
		DbSet<Student> students = await GetDbSetAsync();
		return await students
			.Include(s => s.FatherDetails)
			.Include(s => s.MotherDetails)
			.FirstOrDefaultAsync(s => s.Id == studentId);
	}

	public async Task<List<Student>> GetStudentsAsync(bool includeDetails = false)
	{
		DbSet<Student> students = await GetDbSetAsync();
		return includeDetails
			? await students
				.Include(s => s.FatherDetails)
				.Include(s => s.MotherDetails)
				.ToListAsync()
			: await students
				.ToListAsync();
	}
}
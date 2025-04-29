using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Academic.Curriculum;

public class EfCoreSubjectRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider) : EfCoreRepository<ShikshaMitraDbContext, Subject, Guid>(dbContextProvider), ISubjectRepository
{
    public async Task<PagedResultDto<Subject>> GetSubjectsByClassAsync(Guid classId, int skipCount, int maxResultCount)
    {
        DbSet<Subject> subjects = await GetDbSetAsync();
        var query = subjects.Where(s => s.StandardId == classId)
			.Include(s => s.Teachers)
           .ThenInclude(s => s.Teacher);

        var totalCount = await query.CountAsync();
        List<Subject> items = await query
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync() ?? [];

		return new PagedResultDto<Subject>(
			totalCount,
			items
		);
	}

	public async Task<PagedResultDto<SubjectTeacher>> GetTeachersByStandardAsync(Guid standardId, int skipCount, int maxResultCount)
	{
		DbSet<SubjectTeacher> subjectTeachers = await GetDbSetAsync();
		var query = subjectTeachers.Where(s => s.StandardId == standardId)
			.Include(s => s.Teacher);
		var totalCount = await query.CountAsync();
		List<SubjectTeacher> items = await query
			.Skip(skipCount)
			.Take(maxResultCount)
			.ToListAsync() ?? [];
		return new PagedResultDto<SubjectTeacher>(
			totalCount,
			items
		);
	}
}

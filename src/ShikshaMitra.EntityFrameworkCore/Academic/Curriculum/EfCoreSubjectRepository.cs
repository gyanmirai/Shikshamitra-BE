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

public class EfCoreSubjectRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider) : EfCoreRepository<ShikshaMitraDbContext, TempSubject, Guid>(dbContextProvider), ISubjectRepository
{
    public async Task<PagedResultDto<TempSubject>> GetSubjectsByClassAsync(Guid classId, int skipCount, int maxResultCount)
    {
        DbSet<TempSubject> subjects = await GetDbSetAsync();
        var query = subjects.Where(s => s.StandardId == classId)
			.Include(s => s.Teachers)
           .ThenInclude(s => s.Teacher);

        var totalCount = await query.CountAsync();
        List<TempSubject> items = await query
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync() ?? [];

		return new PagedResultDto<TempSubject>(
			totalCount,
			items
		);
	}
}

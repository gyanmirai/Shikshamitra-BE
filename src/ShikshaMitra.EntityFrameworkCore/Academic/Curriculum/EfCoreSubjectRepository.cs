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

public class EfCoreSubjectRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider) : EfCoreRepository<ShikshaMitraDbContext, StandardSubjectAssignment, Guid>(dbContextProvider), ISubjectRepository
{
    public async Task<PagedResultDto<StandardSubjectAssignment>> GetSubjectsByClassAsync(Guid classId, int skipCount, int maxResultCount)
    {
        DbSet<StandardSubjectAssignment> subjects = await GetDbSetAsync();
        var query = subjects.Where(s => s.StandardId == classId)
			.Include(s => s.Teachers)
           .ThenInclude(s => s.Teacher);

        var totalCount = await query.CountAsync();
        List<StandardSubjectAssignment> items = await query
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync() ?? [];

		return new PagedResultDto<StandardSubjectAssignment>(
			totalCount,
			items
		);
	}
}

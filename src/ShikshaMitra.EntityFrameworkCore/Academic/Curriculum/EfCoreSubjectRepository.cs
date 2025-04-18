using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Academic.Curriculum;

public class EfCoreSubjectRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider) : EfCoreRepository<ShikshaMitraDbContext, Subject, Guid>(dbContextProvider), ISubjectRepository
{
	public async Task<Subject?> GetSubjectsByClassAsync(Guid classId)
	{
		DbSet<Subject> subjects = await GetDbSetAsync();
		return await subjects
			.FirstOrDefaultAsync(s => s.StandardId == classId);
	}
}

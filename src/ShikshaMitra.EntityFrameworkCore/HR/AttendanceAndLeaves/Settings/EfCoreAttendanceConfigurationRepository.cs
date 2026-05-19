using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class EfCoreAttendanceConfigurationRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, AttendanceConfiguration, Guid>(dbContextProvider), IAttendanceConfigurationRepository
{
	public async Task<AttendanceConfiguration?> FindByAcademicYearIdAsync(Guid academicYearId, CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.Include(x => x.Overrides)
			.FirstOrDefaultAsync(x => x.AcademicYearId == academicYearId, cancellationToken);
	}
}

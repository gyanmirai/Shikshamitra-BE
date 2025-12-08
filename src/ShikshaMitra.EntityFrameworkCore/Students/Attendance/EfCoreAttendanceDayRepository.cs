using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.Attendance;
using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.Students.Attendance;

public class EfCoreAttendanceDayRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, AttendanceDay, Guid>(dbContextProvider), IAttendanceDayRepository
{
	public async Task<AttendanceDay?> FindByDateAndDivisionAsync(
		Guid academicYearId,
		Guid standardId,
		Guid divisionId,
		DateTime date,
		bool includeEntries = false,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();
		var query = dbSet.AsQueryable();

		if (includeEntries)
		{
			query = query
				.Include(ad => ad.Entries)
					.ThenInclude(ae => ae.Student);
		}

		return await query
			.FirstOrDefaultAsync(ad =>
				ad.AcademicYearId == academicYearId &&
				ad.StandardId == standardId &&
				ad.DivisionId == divisionId &&
				ad.Date == date.Date,
				cancellationToken);
	}

	public async Task<AttendanceDay?> GetWithEntriesAsync(
		Guid id,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();

		return await dbSet
			.Include(ad => ad.Entries)
				.ThenInclude(ae => ae.Student)
			.FirstOrDefaultAsync(ad => ad.Id == id, cancellationToken);
	}
}

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.HR.AttendanceAndLeaves;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.HR.AttendanceAndLeaves;

public class EfCoreStaffAttendanceDayLockRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, StaffAttendanceDayLock, Guid>(dbContextProvider), IStaffAttendanceDayLockRepository
{
	public async Task<StaffAttendanceDayLock?> FindAsync(
		Guid educationMediumStageId,
		DateOnly lockDate,
		CancellationToken cancellationToken = default)
	{
		var query = await GetQueryableAsync();
		return await query
			.Where(x => x.EducationMediumStageId == educationMediumStageId && x.LockDate == lockDate)
			.FirstOrDefaultAsync(cancellationToken);
	}
}
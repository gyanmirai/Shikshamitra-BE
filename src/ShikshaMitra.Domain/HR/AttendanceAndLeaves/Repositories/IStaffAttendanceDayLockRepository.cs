using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public interface IStaffAttendanceDayLockRepository : IRepository<StaffAttendanceDayLock, Guid>
{
	Task<StaffAttendanceDayLock?> FindAsync(
		DateOnly lockDate,
		CancellationToken cancellationToken = default);
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Attendance;

/// <summary>
/// Repository interface for AttendanceDay
/// </summary>
public interface IAttendanceDayRepository : IRepository<AttendanceDay, Guid>
{
	/// <summary>
	/// Finds an attendance day for a specific Standard+Division and date
	/// </summary>
	Task<AttendanceDay?> FindByDateAndDivisionAsync(
		Guid academicYearId,
		Guid standardId,
		Guid divisionId,
		DateTime date,
		bool includeEntries = false,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets attendance day with all entries included
	/// </summary>
	Task<AttendanceDay?> GetWithEntriesAsync(
		Guid id,
		CancellationToken cancellationToken = default);
}

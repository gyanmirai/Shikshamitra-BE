using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Attendance;

/// <summary>
/// Repository interface for AttendanceEntry
/// </summary>
public interface IAttendanceEntryRepository : IRepository<AttendanceEntry, Guid>
{
	/// <summary>
	/// Gets all attendance entries for a specific attendance day with student details
	/// </summary>
	Task<List<AttendanceEntry>> GetByAttendanceDayWithDetailsAsync(
		Guid attendanceDayId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Finds an attendance entry by AttendanceDay and Student
	/// </summary>
	Task<AttendanceEntry?> FindByAttendanceDayAndStudentAsync(
		Guid attendanceDayId,
		Guid studentId,
		CancellationToken cancellationToken = default);
}

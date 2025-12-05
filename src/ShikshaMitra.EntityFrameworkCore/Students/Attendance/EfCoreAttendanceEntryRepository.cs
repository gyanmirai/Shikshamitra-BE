using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.Attendance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.Students.Attendance;

public class EfCoreAttendanceEntryRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, AttendanceEntry, Guid>(dbContextProvider), IAttendanceEntryRepository
{
	public async Task<List<AttendanceEntry>> GetByAttendanceDayWithDetailsAsync(
		Guid attendanceDayId,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();

		return await dbSet
			.Include(ae => ae.Student)
			.Where(ae => ae.AttendanceDayId == attendanceDayId)
			.ToListAsync(cancellationToken);
	}

	public async Task<AttendanceEntry?> FindByAttendanceDayAndStudentAsync(
		Guid attendanceDayId,
		Guid studentId,
		CancellationToken cancellationToken = default)
	{
		var dbSet = await GetDbSetAsync();

		return await dbSet
			.FirstOrDefaultAsync(ae =>
				ae.AttendanceDayId == attendanceDayId &&
				ae.StudentId == studentId,
				cancellationToken);
	}
}

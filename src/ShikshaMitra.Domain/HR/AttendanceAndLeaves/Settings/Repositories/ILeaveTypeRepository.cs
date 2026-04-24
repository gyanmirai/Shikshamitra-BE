using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public interface ILeaveTypeRepository : IRepository<LeaveType, Guid>
{
	Task<bool> IsTypeExistsAsync(Guid academicYearId, string type, Guid? excludeId = null);

	Task<LeaveType?> FindByTypeAsync(Guid academicYearId, string type);

	Task<List<LeaveType>> GetListByAcademicYearIdAsync(
		Guid academicYearId,
		int skipCount = 0,
		int maxResultCount = int.MaxValue,
		string? sorting = null,
		string? search = null,
		bool includeInactive = false,
		CancellationToken cancellationToken = default);

	Task<long> GetCountByAcademicYearIdAsync(
		Guid academicYearId,
		string? search = null,
		bool includeInactive = false,
		CancellationToken cancellationToken = default);
}

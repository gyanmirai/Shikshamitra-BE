using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public interface IAttendanceConfigurationRepository : IRepository<AttendanceConfiguration, Guid>
{
	Task<AttendanceConfiguration?> FindByAcademicYearIdAsync(Guid academicYearId, CancellationToken cancellationToken = default);
}

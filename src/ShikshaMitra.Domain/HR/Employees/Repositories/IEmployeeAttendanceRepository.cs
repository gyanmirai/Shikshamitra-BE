using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Employees;

public interface IEmployeeAttendanceRepository : IRepository<EmployeeAttendance, Guid>
{
	Task<int> InsertManyIgnoringDuplicatesAsync(
		IReadOnlyCollection<EmployeeAttendance> attendances,
		CancellationToken cancellationToken = default);
}

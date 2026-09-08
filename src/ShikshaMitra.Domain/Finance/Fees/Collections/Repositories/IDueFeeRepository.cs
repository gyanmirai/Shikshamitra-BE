using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

public interface IDueFeeRepository : IRepository<DueFee, Guid>
{
	/// <summary>
	/// Returns true if any due fees have already been generated for the given student.
	/// Used as a defensive idempotency guard inside <see cref="DueFeeManager"/>.
	/// </summary>
	Task<bool> ExistsForStudentAsync(Guid studentId, CancellationToken ct = default);
	Task<bool> ExistsForStudentInPeriodAsync(
		Guid studentId, DateTime periodStart, DateTime periodEnd, CancellationToken ct = default);
}

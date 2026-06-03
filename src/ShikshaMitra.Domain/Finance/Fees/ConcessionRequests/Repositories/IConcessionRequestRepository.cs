using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Concessions;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.ConcessionRequests;

public interface IConcessionRequestRepository : IRepository<ConcessionRequest, Guid>
{
	Task<ConcessionRequest> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

	Task<bool> HasPendingForStudentAndTypeAsync(
		Guid studentId,
		Guid concessionTypeId,
		Guid? excludeRequestId = null,
		CancellationToken cancellationToken = default);

	Task<List<ConcessionRequest>> GetListForApplicationsAsync(
		Guid boardId,
		Guid educationMediumId,
		ConcessionRequestStatus? status,
		int skip,
		int take,
		CancellationToken cancellationToken = default);

	Task<int> CountForApplicationsAsync(
		Guid boardId,
		Guid educationMediumId,
		ConcessionRequestStatus? status,
		CancellationToken cancellationToken = default);

	Task<Dictionary<ConcessionRequestStatus, int>> GetStatusCountsAsync(
		Guid boardId,
		Guid educationMediumId,
		CancellationToken cancellationToken = default);
}

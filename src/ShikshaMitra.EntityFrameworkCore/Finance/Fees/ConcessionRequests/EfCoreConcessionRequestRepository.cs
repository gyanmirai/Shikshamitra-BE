using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Concessions;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.ConcessionRequests;

public class EfCoreConcessionRequestRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, ConcessionRequest, Guid>(dbContextProvider),
		IConcessionRequestRepository
{
	public async Task<ConcessionRequest> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var queryable = await GetQueryableAsync();
		var entity = await queryable.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (entity == null)
		{
			throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(ConcessionRequest), id);
		}

		return entity;
	}

	public async Task<bool> HasPendingForStudentAndTypeAsync(
		Guid studentId,
		Guid concessionTypeId,
		Guid? excludeRequestId = null,
		CancellationToken cancellationToken = default)
	{
		var queryable = await GetQueryableAsync();
		return await queryable.AnyAsync(
			x => x.StudentId == studentId
				&& x.ConcessionTypeId == concessionTypeId
				&& x.Status == ConcessionRequestStatus.Pending
				&& (excludeRequestId == null || x.Id != excludeRequestId.Value),
			cancellationToken);
	}

	public async Task<List<ConcessionRequest>> GetListForApplicationsAsync(
		Guid boardId,
		Guid educationMediumId,
		ConcessionRequestStatus? status,
		int skip,
		int take,
		CancellationToken cancellationToken = default)
	{
		var filtered = await FilterApplicationsAsync(boardId, educationMediumId, status, cancellationToken);
		return filtered
			.OrderByDescending(x => x.CreationTime)
			.Skip(skip)
			.Take(take)
			.ToList();
	}

	public async Task<int> CountForApplicationsAsync(
		Guid boardId,
		Guid educationMediumId,
		ConcessionRequestStatus? status,
		CancellationToken cancellationToken = default)
	{
		var filtered = await FilterApplicationsAsync(boardId, educationMediumId, status, cancellationToken);
		return filtered.Count;
	}

	public async Task<Dictionary<ConcessionRequestStatus, int>> GetStatusCountsAsync(
		Guid boardId,
		Guid educationMediumId,
		CancellationToken cancellationToken = default)
	{
		var filtered = await FilterApplicationsAsync(boardId, educationMediumId, null, cancellationToken);
		return filtered
			.GroupBy(x => x.Status)
			.ToDictionary(g => g.Key, g => g.Count());
	}

	private async Task<List<ConcessionRequest>> FilterApplicationsAsync(
		Guid boardId,
		Guid educationMediumId,
		ConcessionRequestStatus? status,
		CancellationToken cancellationToken)
	{
		var queryable = await GetQueryableAsync();
		var list = await queryable.ToListAsync(cancellationToken);

		return list
			.Where(x => x.GetBoardId() == boardId && x.GetEducationMediumId() == educationMediumId)
			.Where(x => status == null || x.Status == status)
			.ToList();
	}
}

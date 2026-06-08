using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Students;
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
		Guid? standardId,
		Guid? divisionId,
		int skip,
		int take,
		CancellationToken cancellationToken = default)
	{
		var query = await BuildApplicationsQueryAsync(
			boardId, educationMediumId, status, standardId, divisionId, cancellationToken);

		return await query
			.OrderByDescending(x => x.CreationTime)
			.Skip(skip)
			.Take(take)
			.ToListAsync(cancellationToken);
	}

	public async Task<int> CountForApplicationsAsync(
		Guid boardId,
		Guid educationMediumId,
		ConcessionRequestStatus? status,
		Guid? standardId,
		Guid? divisionId,
		CancellationToken cancellationToken = default)
	{
		var query = await BuildApplicationsQueryAsync(
			boardId, educationMediumId, status, standardId, divisionId, cancellationToken);
		return await query.CountAsync(cancellationToken);
	}

	public async Task<Dictionary<ConcessionRequestStatus, int>> GetStatusCountsAsync(
		Guid boardId,
		Guid educationMediumId,
		Guid? standardId,
		Guid? divisionId,
		CancellationToken cancellationToken = default)
	{
		var query = await BuildApplicationsQueryAsync(
			boardId, educationMediumId, status: null, standardId, divisionId, cancellationToken);

		return await query
			.GroupBy(x => x.Status)
			.Select(g => new { Status = g.Key, Count = g.Count() })
			.ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);
	}

	private async Task<IQueryable<ConcessionRequest>> BuildApplicationsQueryAsync(
		Guid boardId,
		Guid educationMediumId,
		ConcessionRequestStatus? status,
		Guid? standardId,
		Guid? divisionId,
		CancellationToken cancellationToken)
	{
		var db = await GetDbContextAsync();
		var queryable = await GetQueryableAsync();

		var studentIds = db.Set<Student>()
			.Where(s => s.AcademicBoardId == boardId && s.AcademicMediumId == educationMediumId);

		if (standardId.HasValue)
		{
			studentIds = studentIds.Where(s => s.StandardId == standardId.Value);
		}

		if (divisionId.HasValue)
		{
			studentIds = studentIds.Where(s => s.DivisionId == divisionId.Value);
		}

		var allowedStudentIds = studentIds.Select(s => s.Id);
		var query = queryable.Where(c => allowedStudentIds.Contains(c.StudentId));

		if (status.HasValue)
		{
			query = query.Where(c => c.Status == status.Value);
		}

		return query;
	}
}

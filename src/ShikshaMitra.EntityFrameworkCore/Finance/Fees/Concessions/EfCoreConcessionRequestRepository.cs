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
using ShikshaMitra.Finance.Common;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.ConcessionRequests;

public partial class EfCoreConcessionRequestRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
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
		Guid academicYearId,
		Guid? excludeRequestId = null,
		CancellationToken cancellationToken = default)
	{
		var queryable = await GetQueryableAsync();
		return await queryable.AnyAsync(
			x => x.StudentId == studentId
				&& x.ConcessionTypeId == concessionTypeId
				&& x.AcademicYearId == academicYearId
				&& x.Status == ConcessionRequestStatus.Pending
				&& (excludeRequestId == null || x.Id != excludeRequestId.Value),
			cancellationToken);
	}

	public async Task<List<ConcessionRequest>> GetListForApplicationsAsync(
		Guid academicYearId,
		Guid boardId,
		Guid educationMediumId,
		ConcessionRequestStatus? status,
		Guid? standardId,
		Guid? divisionId,
		int skip,
		int take,
		IQueryable<Guid>? scopedStudentIdsQuery = null,
		Guid? draftCreatorId = null,
		CancellationToken cancellationToken = default,
        FinanceTableQuery? tableQuery = null, string? tab = null, bool includeReviewableDrafts = false)
	{
		var query = await BuildApplicationsQueryAsync(
			academicYearId, boardId, educationMediumId, status, standardId, divisionId,
			scopedStudentIdsQuery, draftCreatorId, cancellationToken, tableQuery, tab, includeReviewableDrafts);

        query = tableQuery is null ? query.OrderByDescending(x => x.CreationTime).ThenBy(x => x.Id) :
            ConcessionApplicationsTableQuery.Order(query, await GetDbContextAsync(), tableQuery);
		return await query
			.Skip(skip)
			.Take(take)
			.ToListAsync(cancellationToken);
	}

	public async Task<int> CountForApplicationsAsync(
		Guid academicYearId,
		Guid boardId,
		Guid educationMediumId,
		ConcessionRequestStatus? status,
		Guid? standardId,
		Guid? divisionId,
		IQueryable<Guid>? scopedStudentIdsQuery = null,
		Guid? draftCreatorId = null,
		CancellationToken cancellationToken = default,
        FinanceTableQuery? tableQuery = null, string? tab = null, bool includeReviewableDrafts = false)
	{
		var query = await BuildApplicationsQueryAsync(
			academicYearId, boardId, educationMediumId, status, standardId, divisionId,
			scopedStudentIdsQuery, draftCreatorId, cancellationToken, tableQuery, tab, includeReviewableDrafts);
		return await query.CountAsync(cancellationToken);
	}

	public async Task<Dictionary<ConcessionRequestStatus, int>> GetStatusCountsAsync(
		Guid academicYearId,
		Guid boardId,
		Guid educationMediumId,
		Guid? standardId,
		Guid? divisionId,
		IQueryable<Guid>? scopedStudentIdsQuery = null,
		Guid? draftCreatorId = null,
		CancellationToken cancellationToken = default,
        FinanceTableQuery? tableQuery = null, bool includeReviewableDrafts = false)
	{
		var query = await BuildApplicationsQueryAsync(
			academicYearId, boardId, educationMediumId, status: null, standardId, divisionId,
			scopedStudentIdsQuery, draftCreatorId, cancellationToken, tableQuery, includeReviewableDrafts: includeReviewableDrafts);

		return await query
			.GroupBy(x => x.Status)
			.Select(g => new { Status = g.Key, Count = g.Count() })
			.ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);
	}

	private async Task<IQueryable<ConcessionRequest>> BuildApplicationsQueryAsync(
		Guid academicYearId,
		Guid boardId,
		Guid educationMediumId,
		ConcessionRequestStatus? status,
		Guid? standardId,
		Guid? divisionId,
		IQueryable<Guid>? scopedStudentIdsQuery,
		Guid? draftCreatorId,
		CancellationToken cancellationToken,
        FinanceTableQuery? tableQuery = null, string? tab = null, bool includeReviewableDrafts = false)
	{
		var queryable = await GetQueryableAsync();
		var query = queryable.Where(c => c.AcademicYearId == academicYearId &&
			c.BoardId == boardId && c.EducationMediumId == educationMediumId);
		if (standardId.HasValue) query = query.Where(c => c.StandardId == standardId.Value);
		if (divisionId.HasValue) query = query.Where(c => c.DivisionId == divisionId.Value);

		// User scope — restrict to students within the caller's access scope
		if (scopedStudentIdsQuery != null)
			query = query.Where(c => scopedStudentIdsQuery.Contains(c.StudentId));
		if (!includeReviewableDrafts)
			query = draftCreatorId.HasValue
				? query.Where(c => c.Status != ConcessionRequestStatus.Draft || c.CreatorId == draftCreatorId.Value)
				: query.Where(c => c.Status != ConcessionRequestStatus.Draft);

		if (status.HasValue)
		{
			query = query.Where(c => c.Status == status.Value);
		}

        var statuses = ConcessionApplicationsTable.Statuses(tab);
        if (statuses.Length > 0) query = query.Where(c => Enumerable.Contains(statuses, c.Status));
        if (tableQuery is not null)
            query = ConcessionApplicationsTableQuery.Filter(query, await GetDbContextAsync(), tableQuery);
		return query;
	}
}

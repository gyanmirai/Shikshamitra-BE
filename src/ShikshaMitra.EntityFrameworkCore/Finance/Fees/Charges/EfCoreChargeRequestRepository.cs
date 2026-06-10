using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.Charges;

public class EfCoreChargeRequestRepository(
	IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, ChargeRequest, Guid>(dbContextProvider), IChargeRequestRepository
{
	public async Task<ChargeRequestDetailResult?> GetWithLinesAsync(
		Guid chargeRequestId,
		CancellationToken cancellationToken = default)
	{
		var db = await GetDbContextAsync();

		var header = await (
			from request in db.Set<ChargeRequest>().AsNoTracking()
			join category in db.Set<ChargeCategory>().AsNoTracking() on request.ChargeCategoryId equals category.Id
			join academicYear in db.Set<AcademicYear>().AsNoTracking() on request.AcademicYearId equals academicYear.Id
			join requestor in db.Set<IdentityUser>().AsNoTracking() on request.RequestorId equals requestor.Id
			where request.Id == chargeRequestId
			select new { request, category, academicYear, requestor }
		).FirstOrDefaultAsync(cancellationToken);

		if (header is null)
		{
			return null;
		}

		var lines = await (
			from charge in db.Set<Charge>().AsNoTracking()
			join student in db.Set<Student>().AsNoTracking() on charge.StudentId equals student.Id
			join standard in db.Set<Standard>().AsNoTracking() on charge.StandardId equals standard.Id
			join division in db.Set<Division>().AsNoTracking() on charge.DivisionId equals division.Id
			where charge.ChargeRequestId == chargeRequestId
			select new ChargeLineJoinResult(charge, student, standard, division)
		).ToListAsync(cancellationToken);

		return new ChargeRequestDetailResult(
			header.request,
			header.category,
			header.requestor,
			header.academicYear,
			lines);
	}

	public async Task<(List<ChargeRequestJoinResult> Items, int TotalCount)> GetChargeRequestsPagedAsync(
		Guid academicYearId,
		int status,
		int page,
		int pageSize,
		Guid? boardId = null,
		Guid? educationMediumId = null,
		string? search = null,
		CancellationToken cancellationToken = default)
	{
		var db = await GetDbContextAsync();
		var chargeStatus = (ChargeStatus)status;
		var searchLower = search?.ToLower();

		// Step 1: Build a lean filter query on ChargeRequest only (no subqueries in SELECT).
		var filterQuery = db.Set<ChargeRequest>().AsNoTracking()
			.Where(r => r.AcademicYearId == academicYearId);

		if (boardId.HasValue)
			filterQuery = filterQuery.Where(r => r.BoardId == boardId.Value);

		if (educationMediumId.HasValue)
			filterQuery = filterQuery.Where(r => r.EducationMediumId == educationMediumId.Value);

		if (searchLower != null)
		{
			filterQuery = filterQuery.Where(r =>
				EF.Functions.ILike(r.ChargeName, "%" + searchLower + "%") ||
				EF.Functions.ILike(r.PurposeDescription, "%" + searchLower + "%"));
		}

		// Status filter: use EXISTS on Charge lines — translates cleanly without client-side eval.
		var chargeLines = db.Set<Charge>().AsNoTracking().Where(c => c.AcademicYearId == academicYearId);
		filterQuery = chargeStatus switch
		{
			ChargeStatus.Pending => filterQuery.Where(r =>
				chargeLines.Any(c => c.ChargeRequestId == r.Id && c.Status == ChargeStatus.Pending)),
			ChargeStatus.Rejected => filterQuery.Where(r =>
				chargeLines.Any(c => c.ChargeRequestId == r.Id) &&
				!chargeLines.Any(c => c.ChargeRequestId == r.Id && c.Status == ChargeStatus.Pending) &&
				!chargeLines.Any(c => c.ChargeRequestId == r.Id && c.Status == ChargeStatus.Approved)),
			_ => filterQuery.Where(r =>
				chargeLines.Any(c => c.ChargeRequestId == r.Id && c.Status == ChargeStatus.Approved) &&
				!chargeLines.Any(c => c.ChargeRequestId == r.Id && c.Status == ChargeStatus.Pending))
		};

		// Step 2: COUNT on the simple filter query — no correlated subqueries.
		var totalCount = await filterQuery.CountAsync(cancellationToken);

		if (totalCount == 0)
			return ([], 0);

		// Step 3: Page raw ChargeRequest rows ordered by AppliedOn — simple ORDER BY + LIMIT.
		var pagedRequests = await filterQuery
			.OrderByDescending(r => r.AppliedOn)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync(cancellationToken);

		var requestIds = pagedRequests.Select(r => r.Id).ToList();

		// Step 4: Batch-load lookup entities for the paged set only.
		var categoryIds = pagedRequests.Select(r => r.ChargeCategoryId).Distinct().ToList();
		var requestorIds = pagedRequests.Select(r => r.RequestorId).Distinct().ToList();

		var categories = await db.Set<ChargeCategory>().AsNoTracking()
			.Where(c => categoryIds.Contains(c.Id))
			.ToListAsync(cancellationToken);

		var academicYear = await db.Set<AcademicYear>().AsNoTracking()
			.Where(a => a.Id == academicYearId)
			.FirstOrDefaultAsync(cancellationToken);

		var requestors = await db.Set<IdentityUser>().AsNoTracking()
			.Where(u => requestorIds.Contains(u.Id))
			.ToListAsync(cancellationToken);

		// Step 5: Batch aggregate counts per request in a single GROUP BY query.
		var counts = await db.Set<Charge>().AsNoTracking()
			.Where(c => requestIds.Contains(c.ChargeRequestId))
			.GroupBy(c => c.ChargeRequestId)
			.Select(g => new
			{
				RequestId = g.Key,
				Total = g.Count(),
				Pending = g.Count(c => c.Status == ChargeStatus.Pending),
				Approved = g.Count(c => c.Status == ChargeStatus.Approved),
				Rejected = g.Count(c => c.Status == ChargeStatus.Rejected)
			})
			.ToListAsync(cancellationToken);

		// Step 6: Combine in memory — all data is already materialised.
		var categoryMap = categories.ToDictionary(c => c.Id);
		var requestorMap = requestors.ToDictionary(u => u.Id);
		var countMap = counts.ToDictionary(c => c.RequestId);

		var items = pagedRequests.Select(r =>
		{
			countMap.TryGetValue(r.Id, out var cnt);
			categoryMap.TryGetValue(r.ChargeCategoryId, out var cat);
			requestorMap.TryGetValue(r.RequestorId, out var requestor);

			return new ChargeRequestJoinResult(
				r,
				cat!,
				requestor!,
				academicYear!,
				cnt?.Total ?? 0,
				cnt?.Pending ?? 0,
				cnt?.Approved ?? 0,
				cnt?.Rejected ?? 0);
		}).ToList();

		return (items, totalCount);
	}

	public async Task<IQueryable<ChargeLineJoinResult>> GetChargeLinesQueryAsync(
		Guid academicYearId,
		int? status = null,
		Guid? boardId = null,
		Guid? educationMediumId = null)
	{
		var db = await GetDbContextAsync();

		return from charge in db.Set<Charge>().AsNoTracking()
			join student in db.Set<Student>().AsNoTracking() on charge.StudentId equals student.Id
			join standard in db.Set<Standard>().AsNoTracking() on charge.StandardId equals standard.Id
			join division in db.Set<Division>().AsNoTracking() on charge.DivisionId equals division.Id
			where charge.AcademicYearId == academicYearId
			where !status.HasValue || charge.Status == (ChargeStatus)status.Value
			where !boardId.HasValue || charge.BoardId == boardId.Value
			where !educationMediumId.HasValue || charge.EducationMediumId == educationMediumId.Value
			select new ChargeLineJoinResult(charge, student, standard, division);
	}

	public async Task<ChargeRequestStatusCounts> GetStatusCountsAsync(
		Guid academicYearId,
		CancellationToken cancellationToken = default)
	{
		var db = await GetDbContextAsync();

		var lineStats = await db.Set<Charge>().AsNoTracking()
			.Where(c => c.AcademicYearId == academicYearId)
			.GroupBy(c => c.ChargeRequestId)
			.Select(g => new
			{
				PendingCount = g.Count(x => x.Status == ChargeStatus.Pending),
				ApprovedCount = g.Count(x => x.Status == ChargeStatus.Approved),
				RejectedCount = g.Count(x => x.Status == ChargeStatus.Rejected),
				StudentCount = g.Count()
			})
			.ToListAsync(cancellationToken);

		var pending = lineStats.Count(s => s.PendingCount > 0);
		var rejected = lineStats.Count(s => s.PendingCount == 0 && s.RejectedCount == s.StudentCount);
		var approved = lineStats.Count(s => s.PendingCount == 0 && s.ApprovedCount > 0);

		return new ChargeRequestStatusCounts(pending, approved, rejected);
	}
}

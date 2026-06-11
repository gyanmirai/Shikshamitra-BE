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
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.Charges;

public class EfCoreChargeRequestRepository(
	IDbContextProvider<ShikshaMitraDbContext> dbContextProvider,
	IRepository<Charge, Guid> chargeRepository,
	IRepository<ChargeCategory, Guid> chargeCategoryRepository
	)
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
		int status,
		int page,
		int pageSize,
		Guid? boardId = null,
		Guid? educationMediumId = null,
		Guid? standardId = null,
		Guid? divisionId = null,
		string? search = null,
		CancellationToken cancellationToken = default)
	{
		var chargeQuery = await GetQueryableAsync();

		chargeQuery = boardId.HasValue
			? chargeQuery.Where(r => r.BoardId == boardId.Value)
			: chargeQuery.Where(r => r.BoardId != Guid.Empty);

		chargeQuery = educationMediumId.HasValue ?
			chargeQuery.Where(r => r.EducationMediumId == educationMediumId.Value) :
			chargeQuery.Where(r => r.EducationMediumId != Guid.Empty);

		var chargeLineQuery = await chargeRepository.GetQueryableAsync();

		if (standardId.HasValue || divisionId.HasValue)
		{
			if (standardId.HasValue)
				chargeLineQuery = chargeLineQuery.Where(c => c.StandardId == standardId.Value);
			if (divisionId.HasValue)
				chargeLineQuery = chargeLineQuery.Where(c => c.DivisionId == divisionId.Value);
			var requestIds = await chargeLineQuery.Select(c => c.ChargeRequestId).Distinct().ToListAsync(cancellationToken);
			chargeQuery = chargeQuery.Where(r => requestIds.Contains(r.Id));
		}

		// Apply status filter.
		if (status == 0)
		{
			// if status is 0, means get all charges whose at least one line is pending.
			var pendingRequestIds = await chargeLineQuery.Where(c => c.Status == ChargeStatus.Pending)
				.Select(c => c.ChargeRequestId)
				.Distinct()
				.ToListAsync(cancellationToken);
			chargeQuery = chargeQuery.Where(r => pendingRequestIds.Contains(r.Id));
		}
		else
		{
			// if status is 1, means get all charges whose all lines are processed.
			var processedRequestIds = await chargeLineQuery.Where(c => c.Status != ChargeStatus.Pending)
				.Select(c => c.ChargeRequestId)
				.Distinct()
				.ToListAsync(cancellationToken);
			chargeQuery = chargeQuery.Where(r => processedRequestIds.Contains(r.Id));
		}

		// get total count before applying pagination.
		var totalCount = await chargeQuery.CountAsync(cancellationToken);

		// get actual data with pagination using abp code structure
		var charges = await chargeQuery
			.OrderByDescending(r => r.CreationTime)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync(cancellationToken);

		var chargeLines = await chargeLineQuery.OrderByDescending(r => r.CreationTime).ToListAsync(cancellationToken);

		var chargeCategoryIds = charges.Select(c => c.ChargeCategoryId).Distinct().ToList();
		var chargeCategories = await chargeCategoryRepository.GetListAsync(c => chargeCategoryIds.Contains(c.Id));

		// fetch requestor and academic year details if needed (not included in current result projection to avoid complexity, can be added if required)
		var requestorIds = charges.Select(c => c.RequestorId).Distinct().ToList();
		var academicYearIds = charges.Select(c => c.AcademicYearId).Distinct().ToList();

		var requestors = await GetDbContextAsync().Result.Set<IdentityUser>().AsNoTracking()
			.Where(u => requestorIds.Contains(u.Id))
			.ToDictionaryAsync(u => u.Id, cancellationToken);

		var academicYears = await GetDbContextAsync().Result.Set<AcademicYear>().AsNoTracking()
			.Where(a => academicYearIds.Contains(a.Id))
			.ToDictionaryAsync(a => a.Id, cancellationToken);

		// Join charges with categories and project to result.
		var result = from charge in charges
					 join category in chargeCategories on charge.ChargeCategoryId equals category.Id
					 let requestor = requestors.ContainsKey(charge.RequestorId) ? requestors[charge.RequestorId] : null
					 let academicYear = academicYears.ContainsKey(charge.AcademicYearId) ? academicYears[charge.AcademicYearId] : null
					 select new ChargeRequestJoinResult(
						 charge,
						 category,
						 requestor!,
						 academicYear!,
						 chargeLines.Count(cl => cl.ChargeRequestId == charge.Id),
						 chargeLines.Count(cl => cl.ChargeRequestId == charge.Id && cl.Status == ChargeStatus.Pending),
						 chargeLines.Count(cl => cl.ChargeRequestId == charge.Id && cl.Status != ChargeStatus.Pending)
					 );

		return (result.ToList(), totalCount);
	}

	public async Task<IQueryable<ChargeLineJoinResult>> GetChargeLinesQueryAsync(
		int? status = null,
		Guid? boardId = null,
		Guid? educationMediumId = null)
	{
		var db = await GetDbContextAsync();

		return from charge in db.Set<Charge>().AsNoTracking()
			join student in db.Set<Student>().AsNoTracking() on charge.StudentId equals student.Id
			join standard in db.Set<Standard>().AsNoTracking() on charge.StandardId equals standard.Id
			join division in db.Set<Division>().AsNoTracking() on charge.DivisionId equals division.Id
			where !status.HasValue || charge.Status == (ChargeStatus)status.Value
			where !boardId.HasValue || charge.BoardId == boardId.Value
			where !educationMediumId.HasValue || charge.EducationMediumId == educationMediumId.Value
			select new ChargeLineJoinResult(charge, student, standard, division);
	}

	public async Task<ChargeRequestStatusCounts> GetStatusCountsAsync(
		Guid? boardId = null,
		Guid? educationMediumId = null,
		Guid? standardId = null,
		Guid? divisionId = null,
		CancellationToken cancellationToken = default)
	{
		var db = await GetDbContextAsync();

		// Build a base query of charge requests in scope.
		var requestQuery = db.Set<ChargeRequest>().AsNoTracking();

		if (boardId.HasValue)
			requestQuery = requestQuery.Where(r => r.BoardId == boardId.Value);

		if (educationMediumId.HasValue)
			requestQuery = requestQuery.Where(r => r.EducationMediumId == educationMediumId.Value);

		var chargeLines = db.Set<Charge>().AsNoTracking();

		if (standardId.HasValue)
			requestQuery = requestQuery.Where(r =>
				chargeLines.Any(c => c.ChargeRequestId == r.Id && c.StandardId == standardId.Value));

		if (divisionId.HasValue)
			requestQuery = requestQuery.Where(r =>
				chargeLines.Any(c => c.ChargeRequestId == r.Id && c.DivisionId == divisionId.Value));

		// Get all in-scope request IDs.
		var requestIds = await requestQuery.Select(r => r.Id).ToListAsync(cancellationToken);

		if (requestIds.Count == 0)
			return new ChargeRequestStatusCounts(0, 0);

		// Aggregate per-request pending line counts.
		var lineStats = await db.Set<Charge>().AsNoTracking()
			.Where(c => requestIds.Contains(c.ChargeRequestId))
			.GroupBy(c => c.ChargeRequestId)
			.Select(g => new
			{
				HasPending = g.Any(x => x.Status == ChargeStatus.Pending),
			})
			.ToListAsync(cancellationToken);

		var pending = lineStats.Count(s => s.HasPending);
		var processed = lineStats.Count(s => !s.HasPending);

		return new ChargeRequestStatusCounts(pending, processed);
	}
}

using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Collections;
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
		).IgnoreAutoIncludes().FirstOrDefaultAsync(cancellationToken);

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
		).IgnoreAutoIncludes().ToListAsync(cancellationToken);

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
		Guid? standardId = null,
		Guid? divisionId = null,
		string? search = null,
		string? sortBy = null,
		string? sortOrder = null,
		IReadOnlyCollection<Guid>? scopedStudentIds = null,
		CancellationToken cancellationToken = default)
	{
		var chargeQuery = await GetQueryableAsync();
		chargeQuery = chargeQuery.Where(r => r.AcademicYearId == academicYearId);

		chargeQuery = boardId.HasValue
			? chargeQuery.Where(r => r.BoardId == boardId.Value)
			: chargeQuery.Where(r => r.BoardId != Guid.Empty);

		chargeQuery = educationMediumId.HasValue ?
			chargeQuery.Where(r => r.EducationMediumId == educationMediumId.Value) :
			chargeQuery.Where(r => r.EducationMediumId != Guid.Empty);

		var chargeLineQuery = await chargeRepository.GetQueryableAsync();
		chargeLineQuery = chargeLineQuery.Where(c => c.AcademicYearId == academicYearId);
		if (scopedStudentIds is not null)
		{
			if (scopedStudentIds.Count == 0)
				return ([], 0);
			chargeLineQuery = chargeLineQuery.Where(c => scopedStudentIds.Contains(c.StudentId));
			chargeQuery = chargeQuery.Where(r => chargeLineQuery.Any(c => c.ChargeRequestId == r.Id));
		}

		if (!string.IsNullOrWhiteSpace(search))
		{
			var term = search.Trim().ToLower();
			chargeQuery = chargeQuery.Where(r =>
				r.ChargeName.ToLower().Contains(term) ||
				r.PurposeDescription.ToLower().Contains(term));
		}

		if (standardId.HasValue || divisionId.HasValue)
		{
			if (standardId.HasValue)
				chargeLineQuery = chargeLineQuery.Where(c => c.StandardId == standardId.Value);
			if (divisionId.HasValue)
				chargeLineQuery = chargeLineQuery.Where(c => c.DivisionId == divisionId.Value);
			var requestIds = await chargeLineQuery.Select(c => c.ChargeRequestId).Distinct().ToListAsync(cancellationToken);
			chargeQuery = chargeQuery.Where(r => requestIds.Contains(r.Id));
		}

		// Class/division and user scope select requests, but their lifecycle uses the entire batch.
		var candidateIds = await chargeQuery.Select(r => r.Id).ToListAsync(cancellationToken);
		var workflows = await GetWorkflowsAsync(candidateIds, cancellationToken);
		var matchingIds = workflows.Where(x => (int)x.Value == status).Select(x => x.Key).ToList();
		chargeQuery = chargeQuery.Where(r => matchingIds.Contains(r.Id));

		// get total count before applying pagination.
		var totalCount = await chargeQuery.CountAsync(cancellationToken);

		// get actual data with pagination using abp code structure
		var descending = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
		var ordered = (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
		{
			"chargename" => descending ? chargeQuery.OrderByDescending(r => r.ChargeName) : chargeQuery.OrderBy(r => r.ChargeName),
			"amount" => descending ? chargeQuery.OrderByDescending(r => r.Amount) : chargeQuery.OrderBy(r => r.Amount),
			"duedate" => descending ? chargeQuery.OrderByDescending(r => r.DueDate) : chargeQuery.OrderBy(r => r.DueDate),
			"priority" => descending ? chargeQuery.OrderByDescending(r => r.Priority) : chargeQuery.OrderBy(r => r.Priority),
			_ => chargeQuery.OrderByDescending(r => r.CreationTime)
		};
		var charges = await ordered
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync(cancellationToken);

		var chargeLines = await chargeLineQuery.OrderByDescending(r => r.CreationTime).ToListAsync(cancellationToken);

		var chargeCategoryIds = charges.Select(c => c.ChargeCategoryId).Distinct().ToList();
		var chargeCategories = await chargeCategoryRepository.GetListAsync(c => chargeCategoryIds.Contains(c.Id));

		// fetch requestor and academic year details if needed (not included in current result projection to avoid complexity, can be added if required)
		var requestorIds = charges.Select(c => c.RequestorId).Distinct().ToList();
		var academicYearIds = charges.Select(c => c.AcademicYearId).Distinct().ToList();

		var db = await GetDbContextAsync();
		var requestors = await db.Set<IdentityUser>().IgnoreAutoIncludes().AsNoTracking()
			.Where(u => requestorIds.Contains(u.Id))
			.ToDictionaryAsync(u => u.Id, cancellationToken);

		var academicYears = await db.Set<AcademicYear>().IgnoreAutoIncludes().AsNoTracking()
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
						 chargeLines.Count(cl => cl.ChargeRequestId == charge.Id && cl.Status != ChargeStatus.Pending),
						 workflows[charge.Id]
					 );

		return (result.ToList(), totalCount);
	}

	public async Task<IQueryable<ChargeLineJoinResult>> GetChargeLinesQueryAsync(
		Guid academicYearId,
		int? status = null,
		Guid? boardId = null,
		Guid? educationMediumId = null)
	{
		var db = await GetDbContextAsync();

		return from charge in db.Set<Charge>().IgnoreAutoIncludes().AsNoTracking()
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
		Guid? boardId = null,
		Guid? educationMediumId = null,
		Guid? standardId = null,
		Guid? divisionId = null,
		IReadOnlyCollection<Guid>? scopedStudentIds = null,
		CancellationToken cancellationToken = default)
	{
		var db = await GetDbContextAsync();

		// Build a base query of charge requests in scope.
		var requestQuery = db.Set<ChargeRequest>().AsNoTracking()
			.Where(r => r.AcademicYearId == academicYearId);

		if (boardId.HasValue)
			requestQuery = requestQuery.Where(r => r.BoardId == boardId.Value);

		if (educationMediumId.HasValue)
			requestQuery = requestQuery.Where(r => r.EducationMediumId == educationMediumId.Value);

		var chargeLines = db.Set<Charge>().AsNoTracking()
			.Where(c => c.AcademicYearId == academicYearId);

		if (scopedStudentIds is not null)
		{
			if (scopedStudentIds.Count == 0)
				return new ChargeRequestStatusCounts(0, 0);
			chargeLines = chargeLines.Where(c => scopedStudentIds.Contains(c.StudentId));
			requestQuery = requestQuery.Where(r => chargeLines.Any(c => c.ChargeRequestId == r.Id));
		}

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

		var workflows = await GetWorkflowsAsync(requestIds, cancellationToken);
		return new ChargeRequestStatusCounts(
			workflows.Count(x => x.Value == ChargeWorkflowStatus.Pending),
			workflows.Count(x => x.Value == ChargeWorkflowStatus.InProcess),
			workflows.Count(x => x.Value == ChargeWorkflowStatus.Completed),
			workflows.Count(x => x.Value == ChargeWorkflowStatus.Rejected));
	}

	private async Task<Dictionary<Guid, ChargeWorkflowStatus>> GetWorkflowsAsync(
		List<Guid> requestIds, CancellationToken cancellationToken)
	{
		if (requestIds.Count == 0) return [];
		var db = await GetDbContextAsync();
		var lines = await db.Set<Charge>().AsNoTracking()
			.Where(line => requestIds.Contains(line.ChargeRequestId)).ToListAsync(cancellationToken);
		var chargeIds = lines.Select(line => line.Id).ToList();
		var allocations = await (from allocation in db.Set<ChargeCollectionRecord>().AsNoTracking()
			join transaction in db.Set<FeeRecordMetadata>().AsNoTracking()
				on allocation.FeeRecordMetadataId equals transaction.Id
			where chargeIds.Contains(allocation.ChargeId) && transaction.Status != FeeCollectionStatus.Reversed
			select new { allocation.ChargeId, allocation.Amount }).ToListAsync(cancellationToken);
		var collected = allocations.GroupBy(x => x.ChargeId)
			.ToDictionary(g => g.Key, g => g.Sum(x => Convert.ToDecimal(x.Amount)));
		var waivers = await db.Set<ChargeWaiverApplication>().AsNoTracking()
			.Where(x => chargeIds.Contains(x.ChargeId)).ToListAsync(cancellationToken);
		var waived = waivers.GroupBy(x => x.ChargeId)
			.ToDictionary(g => g.Key, g => g.Sum(x => x.EffectiveAmount));
		var linesByRequest = lines.ToLookup(line => line.ChargeRequestId);
		return requestIds.ToDictionary(id => id, id => ChargeWorkflowStatusResolver.Resolve(
			linesByRequest[id].Select(line => new ChargeWorkflowLine(line.Status,
				Math.Max(0m, line.Amount - collected.GetValueOrDefault(line.Id) - waived.GetValueOrDefault(line.Id))))));
	}
}

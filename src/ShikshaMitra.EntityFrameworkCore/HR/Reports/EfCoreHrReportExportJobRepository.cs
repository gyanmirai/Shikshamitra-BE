using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.HR.Reports;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.EntityFrameworkCore.HR.Reports;

public class EfCoreHrReportExportJobRepository(
	IDbContextProvider<ShikshaMitraDbContext> dbContextProvider,
	ICurrentTenant currentTenant)
	: EfCoreRepository<ShikshaMitraDbContext, HrReportExportJob, Guid>(dbContextProvider),
		IHrReportExportJobRepository
{
	public async Task<List<HrReportExportJob>> GetRecentAuthorizedJobsAsync(
		Guid userId,
		int maxResultCount,
		DateTime? requestedFrom = null,
		DateTime? requestedTo = null,
		CancellationToken cancellationToken = default)
	{
		var take = Math.Clamp(maxResultCount <= 0 ? 50 : maxResultCount, 1, 200);
		DateTime? normalizedRequestedFrom = requestedFrom.HasValue
			? NormalizeRequestedBoundary(requestedFrom.Value)
			: null;
		DateTime? normalizedRequestedTo = requestedTo.HasValue
			? NormalizeRequestedBoundary(requestedTo.Value)
			: null;
		var dbContext = await GetDbContextAsync();
		if (IsNpgsql(dbContext))
		{
			return await GetRecentAuthorizedJobsWithJsonbAsync(
				userId,
				take,
				normalizedRequestedFrom,
				normalizedRequestedTo,
				cancellationToken);
		}

		return await GetRecentAuthorizedJobsWithProviderFallbackAsync(
			userId,
			take,
			normalizedRequestedFrom,
			normalizedRequestedTo,
			cancellationToken);
	}

	private async Task<List<HrReportExportJob>> GetRecentAuthorizedJobsWithJsonbAsync(
		Guid userId,
		int take,
		DateTime? requestedFrom,
		DateTime? requestedTo,
		CancellationToken cancellationToken)
	{
		var dbSet = await GetDbSetAsync();
		var sharedUserIdsJson = JsonSerializer.Serialize(new[] { userId });
		var from = ResolveRequestedBoundary(requestedFrom, lowerBound: true);
		var to = ResolveRequestedBoundary(requestedTo, lowerBound: false);
		IQueryable<HrReportExportJob> query;

		if (currentTenant.Id.HasValue)
		{
			var tenantId = currentTenant.Id.Value;
			query = dbSet.FromSqlInterpolated($"""
				SELECT *
				FROM "HR_ReportExportJobs"
				WHERE "IsDeleted" = FALSE
				  AND "TenantId" = {tenantId}
				  AND ("RequestedByUserId" = {userId} OR "SharedUserIdsJson" @> CAST({sharedUserIdsJson} AS jsonb))
				  AND "CreationTime" >= {from}
				  AND "CreationTime" < {to}
				ORDER BY "CreationTime" DESC
				LIMIT {take}
				""");
		}
		else
		{
			query = dbSet.FromSqlInterpolated($"""
				SELECT *
				FROM "HR_ReportExportJobs"
				WHERE "IsDeleted" = FALSE
				  AND "TenantId" IS NULL
				  AND ("RequestedByUserId" = {userId} OR "SharedUserIdsJson" @> CAST({sharedUserIdsJson} AS jsonb))
				  AND "CreationTime" >= {from}
				  AND "CreationTime" < {to}
				ORDER BY "CreationTime" DESC
				LIMIT {take}
				""");
		}

		var jobs = await query.AsNoTracking().ToListAsync(cancellationToken);
		return ExactFilterAndSort(jobs, userId, take, requestedFrom, requestedTo);
	}

	private async Task<List<HrReportExportJob>> GetRecentAuthorizedJobsWithProviderFallbackAsync(
		Guid userId,
		int take,
		DateTime? requestedFrom,
		DateTime? requestedTo,
		CancellationToken cancellationToken)
	{
		var query = await GetQueryableAsync();
		var sharedNeedle = userId.ToString("D");

		var ownedQuery = ApplyRequestedDateFilter(
			query.Where(x => x.RequestedByUserId == userId),
			requestedFrom,
			requestedTo);
		var ownedJobs = await ownedQuery
			.OrderByDescending(x => x.CreationTime)
			.Take(take)
			.ToListAsync(cancellationToken);

		// Provider fallback is used by SQLite/InMemory-style test runs where PostgreSQL JSONB
		// containment is unavailable. Keep exact filtering before the final limit so malformed
		// or false-positive JSON candidates cannot hide a legitimately shared newer job.
		var sharedQuery = ApplyRequestedDateFilter(
			query.Where(x => x.RequestedByUserId != userId && x.SharedUserIdsJson.Contains(sharedNeedle)),
			requestedFrom,
			requestedTo);
		var sharedCandidates = await sharedQuery
			.OrderByDescending(x => x.CreationTime)
			.ToListAsync(cancellationToken);

		return ExactFilterAndSort(
			ownedJobs.Concat(sharedCandidates),
			userId,
			take,
			requestedFrom,
			requestedTo);
	}

	private static List<HrReportExportJob> ExactFilterAndSort(
		IEnumerable<HrReportExportJob> jobs,
		Guid userId,
		int take,
		DateTime? requestedFrom,
		DateTime? requestedTo) =>
		jobs
			.Where(job => job.RequestedByUserId == userId || job.GetSharedUserIds().Contains(userId))
			.Where(job => !requestedFrom.HasValue || job.CreationTime >= requestedFrom.Value)
			.Where(job => !requestedTo.HasValue || job.CreationTime < requestedTo.Value)
			.DistinctBy(job => job.Id)
			.OrderByDescending(job => job.CreationTime)
			.Take(take)
			.ToList();

	private static IQueryable<HrReportExportJob> ApplyRequestedDateFilter(
		IQueryable<HrReportExportJob> query,
		DateTime? requestedFrom,
		DateTime? requestedTo)
	{
		if (requestedFrom.HasValue)
		{
			query = query.Where(job => job.CreationTime >= requestedFrom.Value);
		}

		if (requestedTo.HasValue)
		{
			query = query.Where(job => job.CreationTime < requestedTo.Value);
		}

		return query;
	}

	private static bool IsNpgsql(DbContext dbContext) =>
		dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

	internal static DateTime NormalizeRequestedBoundary(DateTime value) =>
		value.Kind switch
		{
			DateTimeKind.Utc => value,
			DateTimeKind.Local => value.ToUniversalTime(),
			_ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
		};

	internal static DateTime ResolveRequestedBoundary(DateTime? value, bool lowerBound) =>
		value.HasValue
			? NormalizeRequestedBoundary(value.Value)
			: DateTime.SpecifyKind(
				lowerBound ? DateTime.MinValue : DateTime.MaxValue,
				DateTimeKind.Utc);
}

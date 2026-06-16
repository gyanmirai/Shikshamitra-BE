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
		CancellationToken cancellationToken = default)
	{
		var take = Math.Clamp(maxResultCount <= 0 ? 50 : maxResultCount, 1, 200);
		var dbContext = await GetDbContextAsync();
		if (IsNpgsql(dbContext))
		{
			return await GetRecentAuthorizedJobsWithJsonbAsync(userId, take, cancellationToken);
		}

		return await GetRecentAuthorizedJobsWithProviderFallbackAsync(userId, take, cancellationToken);
	}

	private async Task<List<HrReportExportJob>> GetRecentAuthorizedJobsWithJsonbAsync(
		Guid userId,
		int take,
		CancellationToken cancellationToken)
	{
		var dbSet = await GetDbSetAsync();
		var sharedUserIdsJson = JsonSerializer.Serialize(new[] { userId });
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
				ORDER BY "CreationTime" DESC
				LIMIT {take}
				""");
		}

		var jobs = await query.AsNoTracking().ToListAsync(cancellationToken);
		return ExactFilterAndSort(jobs, userId, take);
	}

	private async Task<List<HrReportExportJob>> GetRecentAuthorizedJobsWithProviderFallbackAsync(
		Guid userId,
		int take,
		CancellationToken cancellationToken)
	{
		var query = await GetQueryableAsync();
		var sharedNeedle = userId.ToString("D");

		var ownedJobs = await query
			.Where(x => x.RequestedByUserId == userId)
			.OrderByDescending(x => x.CreationTime)
			.Take(take)
			.ToListAsync(cancellationToken);

		// Provider fallback is used by SQLite/InMemory-style test runs where PostgreSQL JSONB
		// containment is unavailable. Keep exact filtering before the final limit so malformed
		// or false-positive JSON candidates cannot hide a legitimately shared newer job.
		var sharedCandidates = await query
			.Where(x => x.RequestedByUserId != userId && x.SharedUserIdsJson.Contains(sharedNeedle))
			.OrderByDescending(x => x.CreationTime)
			.ToListAsync(cancellationToken);

		return ExactFilterAndSort(ownedJobs.Concat(sharedCandidates), userId, take);
	}

	private static List<HrReportExportJob> ExactFilterAndSort(
		IEnumerable<HrReportExportJob> jobs,
		Guid userId,
		int take) =>
		jobs
			.Where(job => job.RequestedByUserId == userId || job.GetSharedUserIds().Contains(userId))
			.DistinctBy(job => job.Id)
			.OrderByDescending(job => job.CreationTime)
			.Take(take)
			.ToList();

	private static bool IsNpgsql(DbContext dbContext) =>
		dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
}

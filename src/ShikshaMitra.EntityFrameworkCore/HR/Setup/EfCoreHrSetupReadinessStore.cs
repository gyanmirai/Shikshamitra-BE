using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Setup;

public sealed class EfCoreHrSetupReadinessStore(
	IDbContextProvider<ShikshaMitraDbContext> provider,
	ICurrentTenant tenant,
	IUnitOfWorkManager unitOfWork) : IHrSetupReadinessStore, ITransientDependency
{
	private Guid TenantId => tenant.Id ?? throw new InvalidOperationException("HR setup readiness requires a school.");

	public async Task<Guid> ReadRevisionAsync()
	{
		var db = await provider.GetDbContextAsync();
		return await db.Set<HrSetupConfigurationRevision>().Where(row => row.TenantId == TenantId)
			.Select(row => row.Revision).FirstOrDefaultAsync();
	}

	public async Task<bool?> ReadStatusAsync(Guid academicYearId, DateTime nowUtc)
	{
		var db = await provider.GetDbContextAsync();
		var tenantId = TenantId;
		var revisions = db.Set<HrSetupConfigurationRevision>();
		return await db.Set<HrSetupReadinessSnapshot>()
			.Where(row => row.TenantId == tenantId && row.AcademicYearId == academicYearId && row.ValidUntilUtc > nowUtc)
			.Where(row => revisions.Any(revision => revision.TenantId == tenantId && revision.Revision == row.Revision))
			.Select(row => (bool?)row.IsReady).FirstOrDefaultAsync();
	}

	public async Task RecordAsync(Guid academicYearId, Guid revision, bool isReady, DateTime checkedAtUtc, DateTime validUntilUtc)
	{
		var tenantId = TenantId;
		// Acquire the same tenant lock as configuration writes, after all expensive checks have finished.
		using var unit = unitOfWork.Begin(requiresNew: true, isTransactional: true);
		var db = await provider.GetDbContextAsync();
		await db.Database.ExecuteSqlInterpolatedAsync($"""
			INSERT INTO "HR_SetupConfigurationRevisions" ("TenantId", "Revision") VALUES ({tenantId}, {Guid.Empty})
			ON CONFLICT ("TenantId") DO UPDATE SET "Revision" = "HR_SetupConfigurationRevisions"."Revision"
			""");
		if (await ReadRevisionAsync() == revision)
		{
			await db.Database.ExecuteSqlInterpolatedAsync($"""
				INSERT INTO "HR_SetupReadinessSnapshots"
					("TenantId", "AcademicYearId", "Revision", "IsReady", "CheckedAtUtc", "ValidUntilUtc")
				VALUES ({tenantId}, {academicYearId}, {revision}, {isReady}, {checkedAtUtc}, {validUntilUtc})
				ON CONFLICT ("TenantId", "AcademicYearId") DO UPDATE SET
					"Revision" = EXCLUDED."Revision", "IsReady" = EXCLUDED."IsReady",
					"CheckedAtUtc" = EXCLUDED."CheckedAtUtc", "ValidUntilUtc" = EXCLUDED."ValidUntilUtc"
				WHERE "HR_SetupReadinessSnapshots"."CheckedAtUtc" <= EXCLUDED."CheckedAtUtc"
				""");
		}
		await unit.CompleteAsync();
	}
}

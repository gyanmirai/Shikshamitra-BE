using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class EfCoreAdmissionInquiryRepository(
	IDbContextProvider<ShikshaMitraDbContext> dbContextProvider,
	ICurrentTenant currentTenant)
	: EfCoreRepository<ShikshaMitraDbContext, AdmissionInquiry, Guid>(dbContextProvider), IAdmissionInquiryRepository
{
	public async Task<AdmissionInquiry?> FindForApplicationStartAsync(Guid inquiryId, CancellationToken cancellationToken = default)
	{
		var dbContext = await GetDbContextAsync();
		if (dbContext.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
		{
			if (dbContext.Database.CurrentTransaction == null)
				throw new InvalidOperationException("Application start requires a transactional unit of work.");
			// The email worker also updates this aggregate. Keep the inquiry stable until
			// its handoff and the application graph commit together; scope the lock by tenant.
			await dbContext.Database.ExecuteSqlInterpolatedAsync(
				$"""
				SELECT 1 FROM "AppAdmissionInquiries"
				WHERE "Id" = {inquiryId} AND "TenantId" IS NOT DISTINCT FROM {currentTenant.Id} AND NOT "IsDeleted"
				FOR UPDATE
				""", cancellationToken);
		}

		var inquiry = await FindAsync(inquiryId, cancellationToken: cancellationToken);
		if (inquiry == null) return null;
		var entry = dbContext.Entry(inquiry);
		// A prior read in this UOW can still hold the stamp from before another writer
		// committed. Refresh persisted state without discarding a caller's pending changes.
		if (entry.State == EntityState.Unchanged)
			await entry.ReloadAsync(cancellationToken);
		return entry.State == EntityState.Detached || inquiry.IsDeleted || inquiry.TenantId != currentTenant.Id ? null : inquiry;
	}
}

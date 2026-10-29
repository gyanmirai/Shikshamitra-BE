using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Concessions;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.ConcessionRequests;

public partial class EfCoreConcessionRequestRepository
{
	public async Task AcquireBenefitLockAsync(Guid studentId, Guid concessionTypeId, Guid academicYearId,
		CancellationToken cancellationToken = default)
	{
		var db = await GetDbContextAsync();
		if (!db.Database.IsNpgsql()) return;
		if (db.Database.CurrentTransaction == null)
			throw new InvalidOperationException("Concession benefit claims require a transactional unit of work.");
		var tenantId = LazyServiceProvider.LazyGetRequiredService<ICurrentTenant>().Id;
		var key = BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(
			$"finance-concession-benefit:{tenantId}:{studentId}:{academicYearId}:{concessionTypeId}")), 0);
		await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);
	}

	/// <summary>
	/// A Serializable caller may have taken its snapshot before waiting for the claim lock.
	/// Read committed evidence in a short independent UOW, without changing the caller's isolation.
	/// Non-pooled connections avoid starvation when waiting claim transactions fill the main pool.
	/// Callbacks must only read; the outer transaction owns every financial write.
	/// </summary>
	public async Task<T> ReadCommittedAsync<T>(Func<Task<T>> read, CancellationToken cancellationToken = default)
	{
		var outer = await GetDbContextAsync();
		if (!outer.Database.IsNpgsql()) return await read();
		using var fresh = LazyServiceProvider.LazyGetRequiredService<IUnitOfWorkManager>()
			.Begin(new AbpUnitOfWorkOptions { IsTransactional = false }, requiresNew: true);
		var db = await GetDbContextAsync();
		var connectionString = db.Database.GetConnectionString()
			?? throw new InvalidOperationException("A connection string is required for concession benefit validation.");
		db.Database.SetConnectionString(new NpgsqlConnectionStringBuilder(connectionString)
		{
			Pooling = false,
			Enlist = false,
			ApplicationName = "ShikshaMitra concession benefit validation",
		}.ConnectionString);
		var result = await read();
		await fresh.CompleteAsync(cancellationToken);
		return result;
	}

	private async Task<IQueryable<ConcessionRequest>> BlockingBenefitsQueryAsync(Guid studentId, Guid academicYearId,
		Guid? excludeRequestId)
	{
		var tenantId = LazyServiceProvider.LazyGetRequiredService<ICurrentTenant>().Id;
		return (await GetQueryableAsync()).AsNoTracking().Where(x =>
			x.TenantId == tenantId && !x.IsDeleted && x.StudentId == studentId && x.AcademicYearId == academicYearId &&
			(!excludeRequestId.HasValue || x.Id != excludeRequestId.Value) &&
			(x.Status == ConcessionRequestStatus.Pending || x.Status == ConcessionRequestStatus.Approved ||
			 x.Status == ConcessionRequestStatus.PartiallyReversed));
	}

	public async Task<List<ConcessionBenefitConflict>> GetBlockingBenefitsAsync(Guid studentId, Guid academicYearId,
		Guid? excludeRequestId = null, CancellationToken cancellationToken = default) =>
		await (await BlockingBenefitsQueryAsync(studentId, academicYearId, excludeRequestId))
			.OrderBy(x => x.Status == ConcessionRequestStatus.Pending ? 1 : 0).ThenBy(x => x.CreationTime).ThenBy(x => x.Id)
			.Select(x => new ConcessionBenefitConflict(x.Id, x.ConcessionTypeId, x.Status)).ToListAsync(cancellationToken);

	public async Task<ConcessionBenefitConflict?> FindBlockingBenefitAsync(Guid studentId, Guid concessionTypeId,
		Guid academicYearId, Guid? excludeRequestId = null, CancellationToken cancellationToken = default) =>
		await (await BlockingBenefitsQueryAsync(studentId, academicYearId, excludeRequestId))
			.Where(x => x.ConcessionTypeId == concessionTypeId)
			.OrderBy(x => x.Status == ConcessionRequestStatus.Pending ? 1 : 0).ThenBy(x => x.CreationTime).ThenBy(x => x.Id)
			.Select(x => new ConcessionBenefitConflict(x.Id, x.ConcessionTypeId, x.Status)).FirstOrDefaultAsync(cancellationToken);

	public async Task<ConcessionBenefitConflict?> FindBlockingBenefitForMutationAsync(Guid studentId, Guid concessionTypeId,
		Guid academicYearId, Guid? excludeRequestId = null, CancellationToken cancellationToken = default)
	{
		await AcquireBenefitLockAsync(studentId, concessionTypeId, academicYearId, cancellationToken);
		var db = await GetDbContextAsync();
		db.ChangeTracker.DetectChanges();
		var tenantId = LazyServiceProvider.LazyGetRequiredService<ICurrentTenant>().Id;
		// Fresh connections cannot see another claim added/activated in this same transaction.
		var tracked = db.ChangeTracker.Entries<ConcessionRequest>()
			.Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Unchanged).Select(x => x.Entity)
			.Where(x => x.TenantId == tenantId && !x.IsDeleted && x.Id != excludeRequestId && x.StudentId == studentId &&
				x.AcademicYearId == academicYearId && x.ConcessionTypeId == concessionTypeId &&
				(x.Status is ConcessionRequestStatus.Pending or ConcessionRequestStatus.Approved or ConcessionRequestStatus.PartiallyReversed))
			.OrderBy(x => x.Status == ConcessionRequestStatus.Pending ? 1 : 0).ThenBy(x => x.CreationTime).ThenBy(x => x.Id)
			.Select(x => new ConcessionBenefitConflict(x.Id, x.ConcessionTypeId, x.Status)).FirstOrDefault();
		return tracked ?? await ReadCommittedAsync(() => FindBlockingBenefitAsync(studentId, concessionTypeId,
			academicYearId, excludeRequestId, cancellationToken), cancellationToken);
	}
}

using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// EF Core repository implementation for EarningComponent
/// </summary>
public class EfCoreEarningComponentRepository : EfCoreRepository<ShikshaMitraDbContext, EarningComponent, Guid>, IEarningComponentRepository
{
	private const string DependencyGraphLockNamespace = "ShikshaMitra:HR:Payroll:EarningComponentDependencyGraph:";
	private const string DependencyGraphLockUnitOfWorkItemPrefix =
		"ShikshaMitra:HR:Payroll:EarningComponentDependencyGraphLock:";
	private static readonly ConcurrentDictionary<long, SemaphoreSlim> LocalDependencyGraphLocks = new();
	private readonly IUnitOfWorkManager _unitOfWorkManager;

	public EfCoreEarningComponentRepository(
		IDbContextProvider<ShikshaMitraDbContext> dbContextProvider,
		IUnitOfWorkManager unitOfWorkManager)
		: base(dbContextProvider)
	{
		_unitOfWorkManager = unitOfWorkManager;
	}

	public async Task<bool> IsNameExistsAsync(string name, Guid? excludeId = null)
	{
		// Must match aggregate SetName trimming; otherwise availability check can miss a DB collision.
		var normalized = (name ?? string.Empty).Trim().ToLowerInvariant();
		var dbSet = await GetDbSetAsync();
		var query = dbSet.Where(x => x.Name.ToLower() == normalized);

		if (excludeId.HasValue)
		{
			query = query.Where(x => x.Id != excludeId.Value);
		}

		return await query.AnyAsync();
	}

	public async Task<EarningComponent?> FindByNameAsync(string name)
	{
		var normalized = (name ?? string.Empty).Trim().ToLowerInvariant();
		var dbSet = await GetDbSetAsync();
		return await dbSet
			.Where(x => x.Name.ToLower() == normalized)
			.FirstOrDefaultAsync();
	}

	public async Task<IAsyncDisposable> AcquireDependencyGraphMutationLockAsync(
		Guid? tenantId,
		CancellationToken cancellationToken = default)
	{
		if (tenantId != CurrentTenant.Id)
		{
			throw new InvalidOperationException(
				"An earning-component dependency lock can only be acquired for the current tenant.");
		}

		var lockKey = BuildDependencyGraphLockKey(tenantId);
		var currentUnitOfWork = _unitOfWorkManager.Current;
		var unitOfWorkItemKey =
			DependencyGraphLockUnitOfWorkItemPrefix + lockKey.ToString(CultureInfo.InvariantCulture);
		if (currentUnitOfWork?.Items.ContainsKey(unitOfWorkItemKey) == true)
		{
			return NoopAsyncDisposable.Instance;
		}

		var localLock = LocalDependencyGraphLocks.GetOrAdd(lockKey, static _ => new SemaphoreSlim(1, 1));
		await localLock.WaitAsync(cancellationToken);

		try
		{
			var dbContext = await GetDbContextAsync();
			if (IsNpgsql(dbContext))
			{
				if (dbContext.Database.CurrentTransaction is null)
				{
					throw new InvalidOperationException(
						"Earning-component dependency mutations require a transactional unit of work.");
				}

				await dbContext.Database.ExecuteSqlInterpolatedAsync(
					$"SELECT pg_advisory_xact_lock({lockKey})",
					cancellationToken);
			}

			var deferLocalRelease = currentUnitOfWork?.Options.IsTransactional == true;
			var lease = new DependencyGraphMutationLockLease(localLock, !deferLocalRelease);
			if (deferLocalRelease)
			{
				currentUnitOfWork!.Items[unitOfWorkItemKey] = lease;
				currentUnitOfWork.Disposed += (_, _) =>
				{
					currentUnitOfWork.Items.Remove(unitOfWorkItemKey);
					lease.Release();
				};
			}

			return lease;
		}
		catch
		{
			localLock.Release();
			throw;
		}
	}

	private static long BuildDependencyGraphLockKey(Guid? tenantId)
	{
		var material = Encoding.UTF8.GetBytes(
			DependencyGraphLockNamespace + (tenantId?.ToString("N") ?? "host"));
		var digest = SHA256.HashData(material);
		return BinaryPrimitives.ReadInt64BigEndian(digest);
	}

	private static bool IsNpgsql(DbContext dbContext) =>
		dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

	private sealed class DependencyGraphMutationLockLease(
		SemaphoreSlim localLock,
		bool releaseOnDispose) : IAsyncDisposable
	{
		private int _released;

		public ValueTask DisposeAsync()
		{
			if (releaseOnDispose)
			{
				Release();
			}

			return ValueTask.CompletedTask;
		}

		public void Release()
		{
			if (Interlocked.Exchange(ref _released, 1) == 0)
			{
				localLock.Release();
			}
		}
	}

	private sealed class NoopAsyncDisposable : IAsyncDisposable
	{
		public static NoopAsyncDisposable Instance { get; } = new();

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}
}

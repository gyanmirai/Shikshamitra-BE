using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Academic.BackgroundWork;

/// <summary>Bounds background reads and isolates each aggregate's commit and retry.</summary>
public class AcademicBackgroundBatchRunner(
	IDataFilter dataFilter,
	ICurrentTenant currentTenant,
	IUnitOfWorkManager unitOfWorkManager) : DomainService
{
	public const int BatchSize = 100;

	public async Task<int> RunAsync<TEntity>(
		IRepository<TEntity, Guid> repository,
		Expression<Func<TEntity, bool>> candidateFilter,
		Func<TEntity, int> applyChange,
		CancellationToken cancellationToken = default,
		params Expression<Func<TEntity, object>>[] details)
		where TEntity : class, IEntity<Guid>, IMultiTenant
	{
		var changedCount = 0;
		Guid? cursor = null;
		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();
			System.Collections.Generic.List<Candidate> candidates;
			using (var read = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
			using (dataFilter.Disable<IMultiTenant>())
			{
				var query = (await repository.GetQueryableAsync()).Where(candidateFilter);
				if (cursor.HasValue) query = query.Where(entity => entity.Id.CompareTo(cursor.Value) > 0);
				candidates = await AsyncExecuter.ToListAsync(query.OrderBy(entity => entity.Id)
					.Select(entity => new Candidate(entity.Id, entity.TenantId)).Take(BatchSize), cancellationToken);
				await read.CompleteAsync(cancellationToken);
			}
			if (candidates.Count == 0) return changedCount;

			foreach (var candidate in candidates)
			{
				cancellationToken.ThrowIfCancellationRequested();
				try
				{
					using (currentTenant.Change(candidate.TenantId))
					using (dataFilter.Enable<IMultiTenant>())
					using (var write = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
					{
						var query = details.Length == 0 ? await repository.WithDetailsAsync() : await repository.WithDetailsAsync(details);
						var entity = await AsyncExecuter.FirstOrDefaultAsync(query.Where(row => row.Id == candidate.Id), cancellationToken);
						if (entity == null) continue;
						// Revalidate the loaded state; a user or another host may have changed it since the scan.
						var changed = applyChange(entity);
						if (changed > 0) await repository.UpdateAsync(entity, autoSave: false,
							cancellationToken: cancellationToken);
						await write.CompleteAsync(cancellationToken);
						changedCount += changed;
					}
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					throw;
				}
				catch (Exception exception)
				{
					// The failed aggregate remains eligible for the next tick; later IDs still make progress.
					Logger.LogError(exception, "Academic background operation failed for {EntityType} {EntityId} in tenant {TenantId}; retrying on the next run.",
						typeof(TEntity).Name, candidate.Id, candidate.TenantId);
				}
			}
			cursor = candidates[^1].Id;
		}
	}

	private sealed record Candidate(Guid Id, Guid? TenantId);
}

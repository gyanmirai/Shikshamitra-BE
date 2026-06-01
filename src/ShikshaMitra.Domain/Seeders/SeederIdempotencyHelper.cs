using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Seeders;

public class SeederIdempotencyHelper : ISeederIdempotencyHelper, ITransientDependency
{
	public async Task<HashSet<TKey>> PreloadKeysAsync<TEntity, TKey>(
		IRepository<TEntity, Guid> repository,
		Expression<Func<TEntity, bool>> predicate,
		Func<TEntity, TKey> keySelector,
		IEqualityComparer<TKey>? comparer = null)
		where TEntity : class, IEntity<Guid>
	{
		var entities = await repository.GetListAsync(predicate);
		return entities.Select(keySelector).ToHashSet(comparer);
	}

	public async Task<int> InsertMissingAsync<TEntity, TKey>(
		IRepository<TEntity, Guid> repository,
		IEnumerable<TEntity> entities,
		HashSet<TKey> existingKeys,
		Func<TEntity, TKey> keySelector,
		bool autoSave = false)
		where TEntity : class, IEntity<Guid>
	{
		var toInsert = entities
			.Where(entity => existingKeys.Add(keySelector(entity)))
			.ToList();

		if (toInsert.Count == 0)
		{
			return 0;
		}

		await repository.InsertManyAsync(toInsert, autoSave);
		return toInsert.Count;
	}
}

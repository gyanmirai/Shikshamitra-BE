using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Seeders;

public interface ISeederIdempotencyHelper
{
	Task<HashSet<TKey>> PreloadKeysAsync<TEntity, TKey>(
		IRepository<TEntity, Guid> repository,
		Expression<Func<TEntity, bool>> predicate,
		Func<TEntity, TKey> keySelector,
		IEqualityComparer<TKey>? comparer = null)
		where TEntity : class, IEntity<Guid>;

	Task<int> InsertMissingAsync<TEntity, TKey>(
		IRepository<TEntity, Guid> repository,
		IEnumerable<TEntity> entities,
		HashSet<TKey> existingKeys,
		Func<TEntity, TKey> keySelector,
		bool autoSave = false)
		where TEntity : class, IEntity<Guid>;
}

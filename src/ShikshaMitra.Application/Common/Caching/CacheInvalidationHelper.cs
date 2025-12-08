using System;
using System.Collections.Generic;

namespace ShikshaMitra.Application.Common.Caching;

/// <summary>
/// Helper class to generate common cache keys for pagination-based cache invalidation
/// Eliminates code duplication across services
/// </summary>
public static class CacheInvalidationHelper
{
	/// <summary>
	/// Generates common cache keys for pagination scenarios
	/// </summary>
	/// <param name="cacheKeyGenerator">Function to generate cache key from skip/take parameters. Must not be null.</param>
	/// <param name="pageSizes">Common page sizes to generate keys for (default: 10, 25, 50, 100)</param>
	/// <param name="maxPages">Maximum number of pages to generate for each size (default: 10)</param>
	/// <returns>List of cache keys to remove</returns>
	/// <exception cref="ArgumentNullException">Thrown when cacheKeyGenerator is null</exception>
	public static List<string> GenerateCommonCacheKeys(
		Func<int, int, string> cacheKeyGenerator,
		int[]? pageSizes = null,
		int maxPages = 10)
	{
		if (cacheKeyGenerator == null)
		{
			throw new ArgumentNullException(nameof(cacheKeyGenerator));
		}

		pageSizes ??= new[] { 10, 25, 50, 100 };
		var cacheKeysToRemove = new List<string>();

		foreach (var pageSize in pageSizes)
		{
			for (int page = 0; page < maxPages; page++)
			{
				int skipCount = page * pageSize;
				cacheKeysToRemove.Add(cacheKeyGenerator(skipCount, pageSize));
			}
		}

		return cacheKeysToRemove;
	}
}

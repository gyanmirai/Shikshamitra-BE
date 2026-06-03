using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Timing;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Implementation of employee cache manager using ABP's distributed cache.
/// </summary>
public class EmployeeCacheManager : IEmployeeCacheManager, ITransientDependency
{
	private readonly IDistributedCache<EmployeeCacheItem> _cache;
	private readonly IDistributedCache<GuidCacheItem> _userIdToEmployeeIdCache;
	private readonly IDistributedCache<GuidCacheItem> _codeToEmployeeIdCache;
	private readonly IClock _clock;

	private static readonly DistributedCacheEntryOptions CacheOptions = new()
	{
		AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
	};

	private static readonly DistributedCacheEntryOptions LookupCacheOptions = new()
	{
		AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
	};

	public EmployeeCacheManager(
		IDistributedCache<EmployeeCacheItem> cache,
		IDistributedCache<GuidCacheItem> userIdToEmployeeIdCache,
		IDistributedCache<GuidCacheItem> codeToEmployeeIdCache,
		IClock clock)
	{
		_cache = cache;
		_userIdToEmployeeIdCache = userIdToEmployeeIdCache;
		_codeToEmployeeIdCache = codeToEmployeeIdCache;
		_clock = clock;
	}

	public async Task<EmployeeCacheItem?> GetOrAddAsync(
		Guid employeeId,
		Func<Task<Employee?>> factory,
		CancellationToken cancellationToken = default)
	{
		var cacheKey = EmployeeCacheKeyProvider.GetEmployeeByIdKey(employeeId);

		var cached = await _cache.GetAsync(cacheKey, token: cancellationToken);
		if (cached != null)
		{
			return cached;
		}

		var employee = await factory();
		if (employee == null)
		{
			return null;
		}

		var cacheItem = MapToCache(employee, _clock.Now);
		await _cache.SetAsync(cacheKey, cacheItem, CacheOptions, token: cancellationToken);
		return cacheItem;
	}

	public async Task<EmployeeCacheItem?> GetByUserIdOrAddAsync(
		Guid userId,
		Func<Task<Employee?>> factory,
		CancellationToken cancellationToken = default)
	{
		var employeeIdKey = $"e:uid:lookup:{userId}";
		var employeeIdCacheItem = await _userIdToEmployeeIdCache.GetAsync(employeeIdKey, token: cancellationToken);

		if (employeeIdCacheItem != null)
		{
			return await GetOrAddAsync(employeeIdCacheItem.Value, factory, cancellationToken);
		}

		var employee = await factory();
		if (employee == null)
		{
			return null;
		}

		await _userIdToEmployeeIdCache.SetAsync(
			employeeIdKey,
			new GuidCacheItem(employee.Id),
			LookupCacheOptions,
			token: cancellationToken);

		var cacheItem = MapToCache(employee, _clock.Now);
		var mainCacheKey = EmployeeCacheKeyProvider.GetEmployeeByIdKey(employee.Id);
		await _cache.SetAsync(mainCacheKey, cacheItem, CacheOptions, token: cancellationToken);

		return cacheItem;
	}

	public async Task InvalidateEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
	{
		var mainKey = EmployeeCacheKeyProvider.GetEmployeeByIdKey(employeeId);
		await _cache.RemoveAsync(mainKey, token: cancellationToken);
	}

	public async Task InvalidateByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
	{
		var lookupKey = $"e:uid:lookup:{userId}";
		await _userIdToEmployeeIdCache.RemoveAsync(lookupKey, token: cancellationToken);
	}

	public async Task InvalidateByCodeAsync(string employeeCode, CancellationToken cancellationToken = default)
	{
		var lookupKey = $"e:code:lookup:{employeeCode}";
		await _codeToEmployeeIdCache.RemoveAsync(lookupKey, token: cancellationToken);
	}

	public Task ClearAllAsync(CancellationToken cancellationToken = default)
	{
		throw new NotSupportedException(
			"ClearAllAsync is not supported. " +
			"ABP's IDistributedCache does not provide pattern-based key deletion. " +
			"To invalidate employee caches: " +
			"(1) Use InvalidateEmployeeAsync(employeeId) for specific employees, " +
			"(2) Use InvalidateByUserIdAsync(userId) or InvalidateByCodeAsync(code) for lookups, " +
			"(3) Wait for automatic cache expiration (15 minutes for employee data, 5 minutes for lookups), or " +
			"(4) Restart the cache server. " +
			"For bulk cache invalidation, consider implementing a custom Redis-specific solution using the SCAN command.");
	}

	private static EmployeeCacheItem MapToCache(Employee employee, DateTime cachedAt)
	{
		return new EmployeeCacheItem
		{
			Id = employee.Id,
			TenantId = employee.TenantId,
			UserId = employee.UserId,
			EmployeeCode = employee.EmployeeCode,
			FirstName = employee.FirstName,
			LastName = employee.LastName,
			Email = employee.Email,
			MobileNo = employee.MobileNo,
			RoleId = employee.RoleId,
			RoleName = employee.Role?.Name,
			ShiftId = employee.ShiftId,
			ShiftName = employee.Shift?.ShiftName,
			EmployeeStatusType = employee.EmployeeStatusType,
			CachedAt = cachedAt
		};
	}
}

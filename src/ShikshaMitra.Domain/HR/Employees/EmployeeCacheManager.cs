using System;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Microsoft.Extensions.Caching.Distributed;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Cache item for employee data with all auto-included navigations.
/// This is what gets stored in the distributed cache.
/// </summary>
[Serializable]
public class EmployeeCacheItem
{
	public Guid Id { get; set; }
	public Guid? TenantId { get; set; }
	public Guid? UserId { get; set; }
	public string EmployeeCode { get; set; }
	public string? FirstName { get; set; }
	public string? LastName { get; set; }
	public string? Email { get; set; }
	public string? MobileNo { get; set; }
	public Guid RoleId { get; set; }
	public string? RoleName { get; set; }
	public Guid EducationMediumStageId { get; set; }
	public string? EducationMediumStageName { get; set; }
	public Guid? ShiftId { get; set; }
	public string? ShiftName { get; set; }
	public EmployeeStatusType? EmployeeStatusType { get; set; }
	
	// Timestamp for cache validation
	public DateTime CachedAt { get; set; }
}

/// <summary>
/// Cache key provider for employee-related cache items.
/// Provides consistent cache key generation across the application.
/// </summary>
public class EmployeeCacheKeyProvider
{
	/// <summary>
	/// Cache key for employee by ID
	/// Pattern: "e:id:{employeeId}"
	/// </summary>
	public static string GetEmployeeByIdKey(Guid employeeId)
		=> $"e:id:{employeeId}";

	/// <summary>
	/// Cache key for employee by User ID
	/// Pattern: "e:uid:{userId}"
	/// </summary>
	public static string GetEmployeeByUserIdKey(Guid userId)
		=> $"e:uid:{userId}";

	/// <summary>
	/// Cache key for employee by Employee Code
	/// Pattern: "e:code:{employeeCode}"
	/// </summary>
	public static string GetEmployeeByCodeKey(string employeeCode)
		=> $"e:code:{employeeCode}";

	/// <summary>
	/// Cache key pattern for all employee caches for a specific employee
	/// Pattern: "e:*:{employeeId}"
	/// </summary>
	public static string GetEmployeeAllKeysPattern(Guid employeeId)
		=> $"e:*:{employeeId}";
}

/// <summary>
/// Manager for employee cache operations.
/// Handles cache invalidation, retrieval, and storage strategies.
/// </summary>
public interface IEmployeeCacheManager
{
	/// <summary>
	/// Gets employee from cache or loads from database if not cached.
	/// </summary>
	Task<EmployeeCacheItem?> GetOrAddAsync(
		Guid employeeId,
		Func<Task<Employee?>> factory,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets employee by user ID from cache or loads from database.
	/// </summary>
	Task<EmployeeCacheItem?> GetByUserIdOrAddAsync(
		Guid userId,
		Func<Task<Employee?>> factory,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Invalidates all cache entries for a specific employee.
	/// Call this after any employee update operation.
	/// </summary>
	Task InvalidateEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Invalidates cache for an employee by user ID.
	/// </summary>
	Task InvalidateByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Invalidates cache for an employee by employee code.
	/// </summary>
	Task InvalidateByCodeAsync(string employeeCode, CancellationToken cancellationToken = default);

	/// <summary>
	/// Clears all employee caches. Use sparingly (e.g., after bulk operations).
	/// </summary>
	Task ClearAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Wrapper for Guid to make it a reference type for caching.
/// </summary>
[Serializable]
public class GuidCacheItem
{
	public Guid Value { get; set; }

	public GuidCacheItem() { }
	
	public GuidCacheItem(Guid value)
	{
		Value = value;
	}
}

/// <summary>
/// Implementation of employee cache manager using ABP's distributed cache.
/// </summary>
public class EmployeeCacheManager : IEmployeeCacheManager, ITransientDependency
{
	private readonly IDistributedCache<EmployeeCacheItem> _cache;
	private readonly IDistributedCache<GuidCacheItem> _userIdToEmployeeIdCache;
	private readonly IDistributedCache<GuidCacheItem> _codeToEmployeeIdCache;

	// Cache duration: 15 minutes for employee data
	private static readonly DistributedCacheEntryOptions CacheOptions = new()
	{
		AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
	};

	// Shorter cache for lookups: 5 minutes
	private static readonly DistributedCacheEntryOptions LookupCacheOptions = new()
	{
		AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
	};

	public EmployeeCacheManager(
		IDistributedCache<EmployeeCacheItem> cache,
		IDistributedCache<GuidCacheItem> userIdToEmployeeIdCache,
		IDistributedCache<GuidCacheItem> codeToEmployeeIdCache)
	{
		_cache = cache;
		_userIdToEmployeeIdCache = userIdToEmployeeIdCache;
		_codeToEmployeeIdCache = codeToEmployeeIdCache;
	}

	public async Task<EmployeeCacheItem?> GetOrAddAsync(
		Guid employeeId,
		Func<Task<Employee?>> factory,
		CancellationToken cancellationToken = default)
	{
		var cacheKey = EmployeeCacheKeyProvider.GetEmployeeByIdKey(employeeId);

		return await _cache.GetOrAddAsync(
			cacheKey,
			async () =>
			{
				var employee = await factory();
				return employee != null ? MapToCache(employee) : null;
			},
			() => CacheOptions,
			token: cancellationToken
		);
	}

	public async Task<EmployeeCacheItem?> GetByUserIdOrAddAsync(
		Guid userId,
		Func<Task<Employee?>> factory,
		CancellationToken cancellationToken = default)
	{
		// First, try to get employee ID from user ID lookup cache
		var employeeIdKey = $"e:uid:lookup:{userId}";
		var employeeIdCacheItem = await _userIdToEmployeeIdCache.GetAsync(employeeIdKey, token: cancellationToken);

		if (employeeIdCacheItem != null)
		{
			// We have the mapping, get employee from main cache
			return await GetOrAddAsync(employeeIdCacheItem.Value, factory, cancellationToken);
		}

		// Cache miss - load from database
		var employee = await factory();
		if (employee == null)
		{
			return null;
		}

		// Store the user ID to employee ID mapping
		await _userIdToEmployeeIdCache.SetAsync(
			employeeIdKey,
			new GuidCacheItem(employee.Id),
			LookupCacheOptions,
			token: cancellationToken
		);

		// Store the employee data
		var cacheItem = MapToCache(employee);
		var mainCacheKey = EmployeeCacheKeyProvider.GetEmployeeByIdKey(employee.Id);
		await _cache.SetAsync(mainCacheKey, cacheItem, CacheOptions, token: cancellationToken);

		return cacheItem;
	}

	public async Task InvalidateEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
	{
		// Remove main employee cache
		var mainKey = EmployeeCacheKeyProvider.GetEmployeeByIdKey(employeeId);
		await _cache.RemoveAsync(mainKey, token: cancellationToken);

		// Note: We don't remove lookup caches here because we don't have the values
		// They will expire naturally or be overwritten on next access
	}

	public async Task InvalidateByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
	{
		// Remove user ID lookup cache
		var lookupKey = $"e:uid:lookup:{userId}";
		await _userIdToEmployeeIdCache.RemoveAsync(lookupKey, token: cancellationToken);
	}

	public async Task InvalidateByCodeAsync(string employeeCode, CancellationToken cancellationToken = default)
	{
		// Remove employee code lookup cache
		var lookupKey = $"e:code:lookup:{employeeCode}";
		await _codeToEmployeeIdCache.RemoveAsync(lookupKey, token: cancellationToken);
	}

	public Task ClearAllAsync(CancellationToken cancellationToken = default)
	{
		// ClearAll is not supported by ABP's IDistributedCache without pattern matching.
		// Throwing NotSupportedException to prevent misleading behavior where callers
		// think the cache was cleared but nothing actually happened.
		throw new NotSupportedException(
			"ClearAllAsync is not supported. " +
			"ABP's IDistributedCache does not provide pattern-based key deletion. " +
			"To invalidate employee caches: " +
			"(1) Use InvalidateEmployeeAsync(employeeId) for specific employees, " +
			"(2) Use InvalidateByUserIdAsync(userId) or InvalidateByCodeAsync(code) for lookups, " +
			"(3) Wait for automatic cache expiration (15 minutes for employee data, 5 minutes for lookups), or " +
			"(4) Restart the cache server. " +
			"For bulk cache invalidation, consider implementing a custom Redis-specific solution using the SCAN command."
		);
	}

	private static EmployeeCacheItem MapToCache(Employee employee)
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
			EducationMediumStageId = employee.EducationMediumStageId,
			EducationMediumStageName = employee.EducationMediumStage?.Name,
			ShiftId = employee.ShiftId,
			ShiftName = employee.Shift?.ShiftName,
			EmployeeStatusType = employee.EmployeeStatusType,
			CachedAt = DateTime.UtcNow
		};
	}
}

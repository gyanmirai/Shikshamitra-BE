using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using ShikshaMitra.HR.Employees;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Security.UserScope;

public class ReportingManagerAccessService(
	IEmployeeRepository employeeRepository,
	IDistributedCache<ReportingManagerAccessCacheItem> cache)
	: DomainService, IReportingManagerAccessService, ITransientDependency
{
	private static readonly DistributedCacheEntryOptions CacheOptions = new()
	{
		SlidingExpiration = TimeSpan.FromMinutes(15)
	};

	public async Task<bool> HasReporteesAsync(Guid userId)
	{
		return (await GetReporteeEmployeeIdsAsync(userId)).Count > 0;
	}

	public async Task<IReadOnlyCollection<Guid>> GetReporteeEmployeeIdsAsync(Guid userId)
	{
		var cacheKey = GetCacheKey(userId);
		var cached = await cache.GetAsync(cacheKey);
		if (cached != null)
		{
			return cached.ReporteeEmployeeIds;
		}

		var manager = await employeeRepository.GetEmployeeByUserIdAsync(userId);
		if (manager == null)
		{
			await cache.SetAsync(cacheKey, new ReportingManagerAccessCacheItem(), CacheOptions);
			return [];
		}

		var employees = await employeeRepository.GetListAsync();
		var reporteesByManager = employees
			.Where(employee => employee.ReportingManagerId.HasValue)
			.GroupBy(employee => employee.ReportingManagerId!.Value)
			.ToDictionary(group => group.Key, group => group.ToList());
		var visited = new HashSet<Guid> { manager.Id };
		var pendingManagers = new Queue<Guid>();
		var reporteeEmployeeIds = new List<Guid>();
		pendingManagers.Enqueue(manager.Id);

		while (pendingManagers.Count > 0)
		{
			var managerId = pendingManagers.Dequeue();
			if (!reporteesByManager.TryGetValue(managerId, out var directReportees))
			{
				continue;
			}

			foreach (var reportee in directReportees)
			{
				if (visited.Add(reportee.Id))
				{
					reporteeEmployeeIds.Add(reportee.Id);
					pendingManagers.Enqueue(reportee.Id);
				}
			}
		}

		await cache.SetAsync(cacheKey, new ReportingManagerAccessCacheItem
		{
			ReporteeEmployeeIds = reporteeEmployeeIds
		}, CacheOptions);
		return reporteeEmployeeIds;
	}

	public async Task InvalidateAsync(IEnumerable<Guid> userIds)
	{
		foreach (var userId in userIds.Distinct())
		{
			await cache.RemoveAsync(GetCacheKey(userId));
		}
	}

	private string GetCacheKey(Guid userId) => $"ReportingManagerAccess:v1:{CurrentTenant.Id}:{userId}";
}

public class ReportingManagerAccessCacheItem
{
	public List<Guid> ReporteeEmployeeIds { get; set; } = [];
}

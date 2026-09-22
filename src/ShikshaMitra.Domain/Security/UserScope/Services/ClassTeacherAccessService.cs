using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.HR.Employees;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Security.UserScope;

public class ClassTeacherAccessService(
	IRepository<ClassTeacher, Guid> classTeacherRepository,
	IRepository<Employee, Guid> employeeRepository,
	IDistributedCache<ClassTeacherAccessCacheItem> cache)
	: DomainService, IClassTeacherAccessService, ITransientDependency
{
	private static readonly DistributedCacheEntryOptions CacheOptions = new()
	{
		SlidingExpiration = TimeSpan.FromMinutes(15)
	};

	public async Task<bool> HasAssignmentAsync(Guid userId)
	{
		var cacheKey = GetCacheKey(userId);
		var cached = await cache.GetAsync(cacheKey);
		if (cached != null)
		{
			return cached.HasAssignment;
		}

		var employeeIds = (await employeeRepository.GetListAsync(x => x.UserId == userId))
			.Select(x => x.Id)
			.ToList();
		var hasAssignment = employeeIds.Count > 0 &&
			await classTeacherRepository.AnyAsync(x =>
				employeeIds.Contains(x.TeacherId) &&
				x.Status == ClassTeacherAssignmentStatus.Active);

		await cache.SetAsync(cacheKey, new ClassTeacherAccessCacheItem { HasAssignment = hasAssignment }, CacheOptions);
		return hasAssignment;
	}

	public async Task InvalidateAsync(IEnumerable<Guid> userIds)
	{
		foreach (var userId in userIds.Distinct())
		{
			await cache.RemoveAsync(GetCacheKey(userId));
		}
	}

	private string GetCacheKey(Guid userId) => $"ClassTeacherAccess:v1:{CurrentTenant.Id}:{userId}";
}

public class ClassTeacherAccessCacheItem
{
	public bool HasAssignment { get; set; }
}

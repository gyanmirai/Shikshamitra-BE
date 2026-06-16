using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

[RemoteService(false)]
public class LeavePolicyReadService(
	IRepository<LeavePolicy, Guid> repository,
	ILeavePolicyRepository leavePolicyRepository,
	LeavePolicyCacheService cacheService)
	: ApplicationService, ITransientDependency
{
	private const int MaxContractPageSize = 500;

	public async Task<PagedResultDto<LeavePolicyDto>> GetListAsync(PagedAndSortedResultRequestDto input)
	{
		var queryable = await repository.GetQueryableAsync();
		var sorting = input.Sorting ?? "PolicyName ASC";
		queryable = ApplySorting(queryable, sorting);
		var totalCount = await AsyncExecuter.CountAsync(queryable);
		var items = await AsyncExecuter.ToListAsync(queryable.Skip(input.SkipCount).Take(input.MaxResultCount));

		return new PagedResultDto<LeavePolicyDto>(
			totalCount,
			ObjectMapper.Map<List<LeavePolicy>, List<LeavePolicyDto>>(items)
		);
	}

	public async Task<PagedResultDto<LeavePolicyDto>> GetAllActiveAsync(PagedResultRequestDto input)
	{
		var activeCacheKey = cacheService.GetActiveCacheKey(input.SkipCount, input.MaxResultCount);
		var cached = await cacheService.GetActiveAsync(activeCacheKey);
		if (cached != null)
		{
			return cached;
		}

		var queryable = await repository.GetQueryableAsync();
		var query = queryable.Where(x => x.IsActive);
		var totalCount = await AsyncExecuter.CountAsync(query);
		var items = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.PolicyName).Skip(input.SkipCount).Take(input.MaxResultCount));

		var result = new PagedResultDto<LeavePolicyDto>(
			totalCount,
			ObjectMapper.Map<List<LeavePolicy>, List<LeavePolicyDto>>(items)
		);

		await cacheService.SetActiveAsync(activeCacheKey, result);

		return result;
	}

	public async Task<LeavePolicyListResponse> GetListByAcademicYearIdAsync(
		Guid academicYearId,
		int page,
		int pageSize,
		string? search = null,
		CancellationToken cancellationToken = default)
	{
		Check.NotNull(academicYearId, nameof(academicYearId));
		Check.Range(page, nameof(page), 1, int.MaxValue);
		Check.Range(pageSize, nameof(pageSize), 1, MaxContractPageSize);

		var cacheKey = cacheService.GetContractCacheKey(academicYearId, page, pageSize, search);
		var cached = await cacheService.GetContractAsync(cacheKey, cancellationToken);
		if (cached != null)
		{
			return cached;
		}

		var skipCount = (page - 1) * pageSize;
		var items = await leavePolicyRepository.GetListByAcademicYearIdAsync(
			academicYearId,
			skipCount,
			pageSize,
			sorting: "PolicyName ASC",
			search: search,
			includeInactive: false,
			cancellationToken: cancellationToken);

		var totalCount = await leavePolicyRepository.GetCountByAcademicYearIdAsync(
			academicYearId,
			search: search,
			includeInactive: false,
			cancellationToken: cancellationToken);

		var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

		var response = new LeavePolicyListResponse
		{
			TotalCount = (int)totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = Math.Max(1, totalPages),
			Items = items.Select(LeavePolicyProjection.ToPolicyItem).ToList()
		};

		await cacheService.SetContractAsync(cacheKey, response, cancellationToken);

		return response;
	}

	public async Task<LeavePolicyDetailResponse> GetLeavePolicyByIdAsync(Guid policyId)
	{
		var policy = await leavePolicyRepository.GetWithRulesAsync(policyId);
		if (policy == null)
		{
			throw new EntityNotFoundException(typeof(LeavePolicy), policyId);
		}

		return LeavePolicyProjection.ToDetail(policy);
	}

	private static IQueryable<LeavePolicy> ApplySorting(IQueryable<LeavePolicy> queryable, string sorting)
	{
		if (string.IsNullOrWhiteSpace(sorting))
		{
			return queryable.OrderBy(x => x.PolicyName);
		}

		var sortingParts = sorting.Split(',');
		IOrderedQueryable<LeavePolicy>? orderedQueryable = null;
		var isFirstSort = true;

		foreach (var sortPart in sortingParts)
		{
			var parts = sortPart.Trim().Split(' ');
			var property = parts[0];
			var direction = parts.Length > 1 ? parts[1].ToUpperInvariant() : "ASC";

			if (isFirstSort)
			{
				orderedQueryable = property.ToLowerInvariant() switch
				{
					"policyname" => direction == "DESC" ? queryable.OrderByDescending(x => x.PolicyName) : queryable.OrderBy(x => x.PolicyName),
					"isactive" => direction == "DESC" ? queryable.OrderByDescending(x => x.IsActive) : queryable.OrderBy(x => x.IsActive),
					_ => queryable.OrderBy(x => x.PolicyName)
				};
				isFirstSort = false;
			}
			else
			{
				orderedQueryable = property.ToLowerInvariant() switch
				{
					"policyname" => direction == "DESC" ? orderedQueryable!.ThenByDescending(x => x.PolicyName) : orderedQueryable!.ThenBy(x => x.PolicyName),
					"isactive" => direction == "DESC" ? orderedQueryable!.ThenByDescending(x => x.IsActive) : orderedQueryable!.ThenBy(x => x.IsActive),
					_ => orderedQueryable!.ThenBy(x => x.PolicyName)
				};
			}
		}

		return orderedQueryable ?? queryable.OrderBy(x => x.PolicyName);
	}
}

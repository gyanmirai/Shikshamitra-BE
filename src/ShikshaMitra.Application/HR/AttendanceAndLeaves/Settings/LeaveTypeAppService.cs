using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class LeaveTypeAppService : CrudAppService<
	LeaveType,
	LeaveTypeDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateLeaveTypeDto,
	UpdateLeaveTypeDto>, ILeaveTypeAppService
{
	private readonly LeaveTypeManager _leaveTypeManager;
	private readonly ILeaveTypeRepository _leaveTypeRepository;
	private readonly IDistributedCache<PagedResultDto<LeaveTypeDto>> _cache;
	private readonly IDistributedCache<LeaveTypeListResponse> _contractCache;
	
	// Base cache key for all active leave types (unpaginated)
	private string GetBaseCacheKey() => 
		$"EmployeeLeaveTypes:Active:Tenant:{CurrentTenant?.Id ?? Guid.Empty}";
	
	// Paginated cache key
	private string GetActiveCacheKey(int skipCount, int maxResultCount) => 
		$"{GetBaseCacheKey()}:Skip:{skipCount}:Take:{maxResultCount}";
	
	// Contract-specific cache key by mediumStageId
	private string GetContractCacheKey(Guid mediumStageId, int page, int pageSize) =>
		$"EmployeeLeaveTypes:Contract:Tenant:{CurrentTenant?.Id ?? Guid.Empty}:Org:{mediumStageId}:Page:{page}:Size:{pageSize}";

	public LeaveTypeAppService(
		IRepository<LeaveType, Guid> repository,
		ILeaveTypeRepository leaveTypeRepository,
		LeaveTypeManager leaveTypeManager,
		IDistributedCache<PagedResultDto<LeaveTypeDto>> cache,
		IDistributedCache<LeaveTypeListResponse> contractCache)
		: base(repository)
	{
		_leaveTypeRepository = leaveTypeRepository;
		_leaveTypeManager = leaveTypeManager;
		_cache = cache;
		_contractCache = contractCache;
	}

	public override async Task<LeaveTypeDto> CreateAsync(CreateLeaveTypeDto input)
	{
		// Note: This method is kept for backward compatibility
		// Use CreateLeaveTypeAsync for new contract-based operations
		throw new UserFriendlyException("Please use the new CreateLeaveTypeAsync method with mediumStageId");
	}

	public override async Task<LeaveTypeDto> UpdateAsync(Guid id, UpdateLeaveTypeDto input)
	{
		var leaveType = await Repository.GetAsync(id);
		await _leaveTypeManager.UpdateAsync(leaveType, input.Type, input.Color, input.Description);
		await Repository.UpdateAsync(leaveType, autoSave: true);
		await ClearCacheAsync();
		await ClearContractCacheAsync(leaveType.EducationMediumStageId);
		return ObjectMapper.Map<LeaveType, LeaveTypeDto>(leaveType);
	}

	public override async Task DeleteAsync(Guid id)
	{
		var leaveType = await Repository.GetAsync(id);
		leaveType.Deactivate();
		await Repository.UpdateAsync(leaveType, autoSave: true);
		await ClearCacheAsync();
	}

	public override async Task<PagedResultDto<LeaveTypeDto>> GetListAsync(PagedAndSortedResultRequestDto input)
	{
		var queryable = await Repository.GetQueryableAsync();
		
		// Apply sorting
		var sorting = input.Sorting ?? "Type ASC";
		queryable = ApplySorting(queryable, sorting);

		// Get total count
		var totalCount = await AsyncExecuter.CountAsync(queryable);

		// Get items
		var items = await AsyncExecuter.ToListAsync(
			queryable
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount)
		);

		return new PagedResultDto<LeaveTypeDto>(
			totalCount,
			ObjectMapper.Map<List<LeaveType>, List<LeaveTypeDto>>(items)
		);
	}

	public async Task<PagedResultDto<LeaveTypeDto>> GetAllActiveAsync(PagedResultRequestDto input)
	{
		var activeCacheKey = GetActiveCacheKey(input.SkipCount, input.MaxResultCount);
		var cached = await _cache.GetAsync(activeCacheKey);
		if (cached != null)
		{
			return cached;
		}

		var queryable = await Repository.GetQueryableAsync();
		var query = queryable.Where(x => x.IsActive);

		var totalCount = await AsyncExecuter.CountAsync(query);
		var items = await AsyncExecuter.ToListAsync(
			query.OrderBy(x => x.Type)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount)
		);

		var result = new PagedResultDto<LeaveTypeDto>(
			totalCount,
			ObjectMapper.Map<List<LeaveType>, List<LeaveTypeDto>>(items)
		);

		await _cache.SetAsync(
			activeCacheKey,
			result,
			new DistributedCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
			}
		);

		return result;
	}

	/// <summary>
	/// Get paginated leave types for an organization (matches OpenAPI contract)
	/// </summary>
	public async Task<LeaveTypeListResponse> GetListByMediumStageIdAsync(
		Guid mediumStageId, 
		int page, 
		int pageSize,
		CancellationToken cancellationToken = default)
	{
		Check.NotNull(mediumStageId, nameof(mediumStageId));
		Check.Range(page, nameof(page), 1, int.MaxValue);
		Check.Range(pageSize, nameof(pageSize), 1, 100);

		var cacheKey = GetContractCacheKey(mediumStageId, page, pageSize);
		var cached = await _contractCache.GetAsync(cacheKey, token: cancellationToken);
		if (cached != null)
		{
			Logger.LogDebug("Cache hit for leave types. Key: {CacheKey}", cacheKey);
			return cached;
		}

		var skipCount = (page - 1) * pageSize;

		Logger.LogDebug(
			"Fetching leave types. MediumStageId: {MediumStageId}, Page: {Page}, PageSize: {PageSize}, Skip: {Skip}",
			mediumStageId, page, pageSize, skipCount);

		try
		{
			var items = await _leaveTypeRepository.GetListByMediumStageIdAsync(
				mediumStageId,
				skipCount,
				pageSize,
				sorting: "Type ASC",
				includeInactive: false,
				cancellationToken
			);

			var totalCount = await _leaveTypeRepository.GetCountByMediumStageIdAsync(
				mediumStageId,
				includeInactive: false,
				cancellationToken
			);

			Logger.LogDebug(
				"Found {Count} leave types out of {Total} total for MediumStageId: {MediumStageId}",
				items.Count, totalCount, mediumStageId);

			var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

			var response = new LeaveTypeListResponse
			{
				TotalCount = (int)totalCount,
				CurrentPage = page,
				PageSize = pageSize,
				TotalPages = Math.Max(1, totalPages),
				Items = items.Select(lt => new LeaveTypeItem
				{
					Id = lt.Id,
					Name = lt.Type,
					Description = lt.Description,
					Color = lt.Color
				}).ToList()
			};

			await _contractCache.SetAsync(
				cacheKey,
				response,
				new DistributedCacheEntryOptions
				{
					AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
				},
				token: cancellationToken
			);

			return response;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, 
				"Failed to fetch leave types for MediumStageId: {MediumStageId}, Page: {Page}, PageSize: {PageSize}",
				mediumStageId, page, pageSize);
			throw;
		}
	}

	/// <summary>
	/// Create a leave type (matches OpenAPI contract)
	/// </summary>
	public async Task<LeaveTypeResponse> CreateLeaveTypeAsync(LeaveTypeCreateRequest input)
	{
		var leaveType = await _leaveTypeManager.CreateAsync(
			input.MediumStageId,
			input.Name,
			input.Color,
			input.Description
		);

		await Repository.InsertAsync(leaveType, autoSave: true);
		await ClearCacheAsync();
		await ClearContractCacheAsync(input.MediumStageId);

		return new LeaveTypeResponse
		{
			Id = leaveType.Id,
			Name = leaveType.Type,
			Description = leaveType.Description,
			Color = leaveType.Color
		};
	}

	/// <summary>
	/// Get leave type by ID (matches OpenAPI contract)
	/// </summary>
	public async Task<LeaveTypeResponse> GetLeaveTypeByIdAsync(Guid leaveTypeId)
	{
		// Repository.GetAsync throws EntityNotFoundException if not found
		var leaveType = await Repository.GetAsync(leaveTypeId);

		return new LeaveTypeResponse
		{
			Id = leaveType.Id,
			Name = leaveType.Type,
			Description = leaveType.Description,
			Color = leaveType.Color
		};
	}

	/// <summary>
	/// Update a leave type (matches OpenAPI contract)
	/// </summary>
	public async Task<LeaveTypeResponse> UpdateLeaveTypeAsync(Guid leaveTypeId, LeaveTypeUpdateRequest input)
	{
		// Repository.GetAsync throws EntityNotFoundException if not found
		var leaveType = await Repository.GetAsync(leaveTypeId);

		// Apply partial updates - only update fields that are provided
		var newType = input.Name ?? leaveType.Type;
		var newColor = input.Color ?? leaveType.Color;
		var newDescription = input.Description; // Can be null to clear

		await _leaveTypeManager.UpdateAsync(leaveType, newType, newColor, newDescription);
		await Repository.UpdateAsync(leaveType, autoSave: true);
		await ClearCacheAsync();
		await ClearContractCacheAsync(leaveType.EducationMediumStageId);

		return new LeaveTypeResponse
		{
			Id = leaveType.Id,
			Name = leaveType.Type,
			Description = leaveType.Description,
			Color = leaveType.Color
		};
	}

	/// <summary>
	/// Delete (deactivate) a leave type (matches OpenAPI contract)
	/// </summary>
	public async Task<LeaveTypeDeleteResponse> DeleteLeaveTypeAsync(Guid leaveTypeId)
	{
		// Repository.GetAsync throws EntityNotFoundException if not found
		var leaveType = await Repository.GetAsync(leaveTypeId);

		leaveType.Deactivate();
		await Repository.UpdateAsync(leaveType, autoSave: true);
		await ClearCacheAsync();
		await ClearContractCacheAsync(leaveType.EducationMediumStageId);

		return new LeaveTypeDeleteResponse
		{
			Id = leaveTypeId,
			Message = "Leave type deleted successfully"
		};
	}

	private async Task ClearCacheAsync()
	{
		// Clear cache by removing common pagination combinations
		var baseCacheKey = GetBaseCacheKey();
		var cacheKeysToRemove = new List<string>();
		
		int[] commonPageSizes = { 10, 25, 50, 100 };
		int maxPages = 10;
		
		foreach (var pageSize in commonPageSizes)
		{
			for (int page = 0; page < maxPages; page++)
			{
				int skipCount = page * pageSize;
				cacheKeysToRemove.Add(GetActiveCacheKey(skipCount, pageSize));
			}
		}
		
		await _cache.RemoveManyAsync(cacheKeysToRemove);
	}

	private async Task ClearContractCacheAsync(Guid mediumStageId)
	{
		// Clear contract cache for the specific organization
		var cacheKeysToRemove = new List<string>();
		int[] commonPageSizes = { 10, 25, 50, 100 };
		int maxPages = 10;
		
		foreach (var pageSize in commonPageSizes)
		{
			for (int page = 1; page <= maxPages; page++)
			{
				cacheKeysToRemove.Add(GetContractCacheKey(mediumStageId, page, pageSize));
			}
		}
		
		await _contractCache.RemoveManyAsync(cacheKeysToRemove);
	}

	private IQueryable<LeaveType> ApplySorting(IQueryable<LeaveType> queryable, string sorting)
	{
		if (string.IsNullOrWhiteSpace(sorting))
		{
			return queryable.OrderBy(x => x.Type);
		}

		var sortingParts = sorting.Split(',');
		IOrderedQueryable<LeaveType>? orderedQueryable = null;
		bool isFirstSort = true;

		foreach (var sortPart in sortingParts)
		{
			var parts = sortPart.Trim().Split(' ');
			var property = parts[0];
			var direction = parts.Length > 1 ? parts[1].ToUpper() : "ASC";

			if (isFirstSort)
			{
				orderedQueryable = property.ToLower() switch
				{
					"type" => direction == "DESC" 
						? queryable.OrderByDescending(x => x.Type) 
						: queryable.OrderBy(x => x.Type),
					"color" => direction == "DESC"
						? queryable.OrderByDescending(x => x.Color)
						: queryable.OrderBy(x => x.Color),
					"isactive" => direction == "DESC"
						? queryable.OrderByDescending(x => x.IsActive)
						: queryable.OrderBy(x => x.IsActive),
					_ => queryable.OrderBy(x => x.Type)
				};
				isFirstSort = false;
			}
			else
			{
				orderedQueryable = property.ToLower() switch
				{
					"type" => direction == "DESC" 
						? orderedQueryable!.ThenByDescending(x => x.Type) 
						: orderedQueryable!.ThenBy(x => x.Type),
					"color" => direction == "DESC"
						? orderedQueryable!.ThenByDescending(x => x.Color)
						: orderedQueryable!.ThenBy(x => x.Color),
					"isactive" => direction == "DESC"
						? orderedQueryable!.ThenByDescending(x => x.IsActive)
						: orderedQueryable!.ThenBy(x => x.IsActive),
					_ => orderedQueryable!.ThenBy(x => x.Type)
				};
			}
		}

		return orderedQueryable ?? queryable.OrderBy(x => x.Type);
	}
}

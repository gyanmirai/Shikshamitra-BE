using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

[RemoteService(false)]
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

	private string GetBaseCacheKey() =>
		$"EmployeeLeaveTypes:Active:Tenant:{CurrentTenant?.Id ?? Guid.Empty}";

	private string GetActiveCacheKey(int skipCount, int maxResultCount) =>
		$"{GetBaseCacheKey()}:Skip:{skipCount}:Take:{maxResultCount}";

	private string GetContractCacheKey(Guid academicYearId, int page, int pageSize, string? search) =>
		$"EmployeeLeaveTypes:Contract:Tenant:{CurrentTenant?.Id ?? Guid.Empty}:AcademicYear:{academicYearId}:Page:{page}:Size:{pageSize}:Search:{search ?? string.Empty}";

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

	public override Task<LeaveTypeDto> CreateAsync(CreateLeaveTypeDto input)
	{
		throw new UserFriendlyException("Please use CreateLeaveTypeAsync with academicYearId.");
	}

	public override async Task<LeaveTypeDto> UpdateAsync(Guid id, UpdateLeaveTypeDto input)
	{
		var leaveType = await Repository.GetAsync(id);
		await _leaveTypeManager.UpdateAsync(leaveType, input.Type, input.Color, input.Description);
		await Repository.UpdateAsync(leaveType, autoSave: true);
		await ClearCacheAsync();
		await ClearContractCacheAsync(leaveType.AcademicYearId);
		return ObjectMapper.Map<LeaveType, LeaveTypeDto>(leaveType);
	}

	public override async Task DeleteAsync(Guid id)
	{
		var leaveType = await Repository.GetAsync(id);
		leaveType.Deactivate();
		await Repository.UpdateAsync(leaveType, autoSave: true);
		await ClearCacheAsync();
		await ClearContractCacheAsync(leaveType.AcademicYearId);
	}

	public override async Task<PagedResultDto<LeaveTypeDto>> GetListAsync(PagedAndSortedResultRequestDto input)
	{
		var queryable = await Repository.GetQueryableAsync();
		var sorting = input.Sorting ?? "Type ASC";
		queryable = ApplySorting(queryable, sorting);
		var totalCount = await AsyncExecuter.CountAsync(queryable);
		var items = await AsyncExecuter.ToListAsync(queryable.Skip(input.SkipCount).Take(input.MaxResultCount));

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
		var items = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.Type).Skip(input.SkipCount).Take(input.MaxResultCount));

		var result = new PagedResultDto<LeaveTypeDto>(
			totalCount,
			ObjectMapper.Map<List<LeaveType>, List<LeaveTypeDto>>(items)
		);

		await _cache.SetAsync(
			activeCacheKey,
			result,
			new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) }
		);

		return result;
	}

	public async Task<LeaveTypeListResponse> GetListByAcademicYearIdAsync(
		Guid academicYearId,
		int page,
		int pageSize,
		string? search = null,
		CancellationToken cancellationToken = default)
	{
		Check.NotNull(academicYearId, nameof(academicYearId));
		Check.Range(page, nameof(page), 1, int.MaxValue);
		Check.Range(pageSize, nameof(pageSize), 1, 100);

		var cacheKey = GetContractCacheKey(academicYearId, page, pageSize, search);
		var cached = await _contractCache.GetAsync(cacheKey, token: cancellationToken);
		if (cached != null)
		{
			return cached;
		}

		var skipCount = (page - 1) * pageSize;
		var items = await _leaveTypeRepository.GetListByAcademicYearIdAsync(
			academicYearId,
			skipCount,
			pageSize,
			sorting: "Type ASC",
			search: search,
			includeInactive: false,
			cancellationToken: cancellationToken);

		var totalCount = await _leaveTypeRepository.GetCountByAcademicYearIdAsync(
			academicYearId,
			search: search,
			includeInactive: false,
			cancellationToken: cancellationToken);

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
			new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) },
			token: cancellationToken);

		return response;
	}

	public async Task<LeaveTypeResponse> CreateLeaveTypeAsync(Guid academicYearId, LeaveTypeCreateRequest input)
	{
		var leaveType = await _leaveTypeManager.CreateAsync(
			academicYearId,
			input.Name,
			input.Color,
			input.Description);

		await Repository.InsertAsync(leaveType, autoSave: true);
		await ClearCacheAsync();
		await ClearContractCacheAsync(academicYearId);

		return new LeaveTypeResponse
		{
			Id = leaveType.Id,
			Name = leaveType.Type,
			Description = leaveType.Description,
			Color = leaveType.Color
		};
	}

	public async Task<LeaveTypeResponse> GetLeaveTypeByIdAsync(Guid leaveTypeId)
	{
		var leaveType = await Repository.GetAsync(leaveTypeId);
		return new LeaveTypeResponse
		{
			Id = leaveType.Id,
			Name = leaveType.Type,
			Description = leaveType.Description,
			Color = leaveType.Color
		};
	}

	public async Task<LeaveTypeResponse> UpdateLeaveTypeAsync(Guid leaveTypeId, LeaveTypeUpdateRequest input)
	{
		var leaveType = await Repository.GetAsync(leaveTypeId);
		var newType = input.Name ?? leaveType.Type;
		var newColor = input.Color ?? leaveType.Color;
		var newDescription = input.Description;

		await _leaveTypeManager.UpdateAsync(leaveType, newType, newColor, newDescription);
		await Repository.UpdateAsync(leaveType, autoSave: true);
		await ClearCacheAsync();
		await ClearContractCacheAsync(leaveType.AcademicYearId);

		return new LeaveTypeResponse
		{
			Id = leaveType.Id,
			Name = leaveType.Type,
			Description = leaveType.Description,
			Color = leaveType.Color
		};
	}

	public async Task<LeaveTypeDeleteResponse> DeleteLeaveTypeAsync(Guid leaveTypeId)
	{
		var leaveType = await Repository.GetAsync(leaveTypeId);
		leaveType.Deactivate();
		await Repository.UpdateAsync(leaveType, autoSave: true);
		await ClearCacheAsync();
		await ClearContractCacheAsync(leaveType.AcademicYearId);

		return new LeaveTypeDeleteResponse { Id = leaveTypeId, Message = "Leave type deleted successfully" };
	}

	private async Task ClearCacheAsync()
	{
		var cacheKeysToRemove = new List<string>();
		int[] commonPageSizes = { 10, 25, 50, 100 };
		const int maxPages = 10;

		foreach (var pageSize in commonPageSizes)
		{
			for (int page = 0; page < maxPages; page++)
			{
				cacheKeysToRemove.Add(GetActiveCacheKey(page * pageSize, pageSize));
			}
		}

		await _cache.RemoveManyAsync(cacheKeysToRemove);
	}

	private async Task ClearContractCacheAsync(Guid academicYearId)
	{
		var cacheKeysToRemove = new List<string>();
		int[] commonPageSizes = { 10, 25, 50, 100 };
		const int maxPages = 10;

		foreach (var pageSize in commonPageSizes)
		{
			for (int page = 1; page <= maxPages; page++)
			{
				cacheKeysToRemove.Add(GetContractCacheKey(academicYearId, page, pageSize, null));
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
					"type" => direction == "DESC" ? queryable.OrderByDescending(x => x.Type) : queryable.OrderBy(x => x.Type),
					"color" => direction == "DESC" ? queryable.OrderByDescending(x => x.Color) : queryable.OrderBy(x => x.Color),
					"isactive" => direction == "DESC" ? queryable.OrderByDescending(x => x.IsActive) : queryable.OrderBy(x => x.IsActive),
					_ => queryable.OrderBy(x => x.Type)
				};
				isFirstSort = false;
			}
			else
			{
				orderedQueryable = property.ToLowerInvariant() switch
				{
					"type" => direction == "DESC" ? orderedQueryable!.ThenByDescending(x => x.Type) : orderedQueryable!.ThenBy(x => x.Type),
					"color" => direction == "DESC" ? orderedQueryable!.ThenByDescending(x => x.Color) : orderedQueryable!.ThenBy(x => x.Color),
					"isactive" => direction == "DESC" ? orderedQueryable!.ThenByDescending(x => x.IsActive) : orderedQueryable!.ThenBy(x => x.IsActive),
					_ => orderedQueryable!.ThenBy(x => x.Type)
				};
			}
		}

		return orderedQueryable ?? queryable.OrderBy(x => x.Type);
	}
}

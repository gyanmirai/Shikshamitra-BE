using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class LeavePolicyAppService : CrudAppService<
	LeavePolicy,
	LeavePolicyDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateLeavePolicyDto,
	UpdateLeavePolicyDto>, ILeavePolicyAppService
{
	private readonly LeavePolicyManager _leavePolicyManager;
	private readonly ILeavePolicyRepository _leavePolicyRepository;
	private readonly IDistributedCache<PagedResultDto<LeavePolicyDto>> _cache;
	private readonly IDistributedCache<LeavePolicyListResponse> _contractCache;
	
	// Base cache key for all active leave policies
	private string GetBaseCacheKey() => 
		$"EmployeeLeavePolicies:Active:Tenant:{CurrentTenant?.Id ?? Guid.Empty}";
	
	// Paginated cache key
	private string GetActiveCacheKey(int skipCount, int maxResultCount) => 
		$"{GetBaseCacheKey()}:Skip:{skipCount}:Take:{maxResultCount}";
	
	// Contract-specific cache key by mediumStageId
	private string GetContractCacheKey(Guid mediumStageId, int page, int pageSize) =>
		$"EmployeeLeavePolicies:Contract:Tenant:{CurrentTenant?.Id ?? Guid.Empty}:Org:{mediumStageId}:Page:{page}:Size:{pageSize}";

	public LeavePolicyAppService(
		IRepository<LeavePolicy, Guid> repository,
		ILeavePolicyRepository leavePolicyRepository,
		LeavePolicyManager leavePolicyManager,
		IDistributedCache<PagedResultDto<LeavePolicyDto>> cache,
		IDistributedCache<LeavePolicyListResponse> contractCache)
		: base(repository)
	{
		_leavePolicyRepository = leavePolicyRepository;
		_leavePolicyManager = leavePolicyManager;
		_cache = cache;
		_contractCache = contractCache;
	}

	public override async Task<LeavePolicyDto> CreateAsync(CreateLeavePolicyDto input)
	{
		// Note: This method is kept for backward compatibility
		// Use CreateLeavePolicyAsync for new contract-based operations
		throw new UserFriendlyException("Please use the new CreateLeavePolicyAsync method with mediumStageId");
	}

	public override async Task<LeavePolicyDto> UpdateAsync(Guid id, UpdateLeavePolicyDto input)
	{
		// Note: This method is kept for backward compatibility
		throw new UserFriendlyException("Please use the new UpdateLeavePolicyAsync method");
	}

	public override async Task DeleteAsync(Guid id)
	{
		// Use soft delete to preserve historical data integrity
		var leavePolicy = await Repository.GetAsync(id);
		leavePolicy.Deactivate();
		await Repository.UpdateAsync(leavePolicy, autoSave: true);
		await ClearCacheAsync();
		await ClearContractCacheAsync(leavePolicy.EducationMediumStageId);
	}

	public override async Task<PagedResultDto<LeavePolicyDto>> GetListAsync(PagedAndSortedResultRequestDto input)
	{
		var queryable = await Repository.GetQueryableAsync();
		
		// Apply sorting
		var sorting = input.Sorting ?? "PolicyName ASC";
		queryable = ApplySorting(queryable, sorting);

		// Get total count
		var totalCount = await AsyncExecuter.CountAsync(queryable);

		// Get items
		var items = await AsyncExecuter.ToListAsync(
			queryable
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount)
		);

		return new PagedResultDto<LeavePolicyDto>(
			totalCount,
			ObjectMapper.Map<List<LeavePolicy>, List<LeavePolicyDto>>(items)
		);
	}

	public async Task<PagedResultDto<LeavePolicyDto>> GetAllActiveAsync(PagedResultRequestDto input)
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
			query.OrderBy(x => x.PolicyName)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount)
		);

		var result = new PagedResultDto<LeavePolicyDto>(
			totalCount,
			ObjectMapper.Map<List<LeavePolicy>, List<LeavePolicyDto>>(items)
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
	/// Get paginated leave policies for an organization (matches OpenAPI contract)
	/// </summary>
	public async Task<LeavePolicyListResponse> GetListByMediumStageIdAsync(Guid mediumStageId, int page, int pageSize)
	{
		var cacheKey = GetContractCacheKey(mediumStageId, page, pageSize);
		var cached = await _contractCache.GetAsync(cacheKey);
		if (cached != null)
		{
			return cached;
		}

		var skipCount = (page - 1) * pageSize;
		
		var items = await _leavePolicyRepository.GetListByMediumStageIdAsync(
			mediumStageId,
			skipCount,
			pageSize
		);

		var totalCount = await _leavePolicyRepository.GetCountByMediumStageIdAsync(mediumStageId);
		var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

		var rowNumber = skipCount;
		var response = new LeavePolicyListResponse
		{
			TotalCount = (int)totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = Math.Max(1, totalPages),
			Items = items.Select(lp => new LeavePolicyItem
			{
				Id = ++rowNumber, // Sequential row number for display
				PolicyId = lp.Id,
				Title = lp.PolicyName,
				LeaveType = lp.LeaveType?.Type ?? "Unknown",
				MaximumPerYear = lp.MaximumDaysPerYear,
				CarryForward = lp.CarryForward ? "Yes" : "No",
				Encashable = lp.Encashable ? "Yes" : "No",
				PaidType = lp.PaidType == LeavePaidType.Paid ? "Paid" : "Unpaid",
				RequiresApproval = lp.RequiresApproval ? "Yes" : "No"
			}).ToList()
		};

		await _contractCache.SetAsync(
			cacheKey,
			response,
			new DistributedCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
			}
		);

		return response;
	}

	/// <summary>
	/// Create a leave policy (matches OpenAPI contract)
	/// </summary>
	public async Task<LeavePolicyResponse> CreateLeavePolicyAsync(LeavePolicyCreateRequest input)
	{
		var paidType = input.PaidType == "Paid" ? LeavePaidType.Paid : LeavePaidType.Unpaid;

		var policy = await _leavePolicyManager.CreateAsync(
			input.MediumStageId,
			input.PolicyTitle,
			input.LeaveType,
			input.MaxLeavePerYear,
			paidType,
			input.CarryForward,
			input.EncashmentAllowed,
			input.RequiresApproval,
			input.Description
		);

		await Repository.InsertAsync(policy, autoSave: true);
		await ClearCacheAsync();
		await ClearContractCacheAsync(input.MediumStageId);

		return new LeavePolicyResponse
		{
			Id = policy.Id.ToString(),
			PolicyTitle = policy.PolicyName,
			LeaveType = policy.LeaveTypeId,
			MaxLeavePerYear = policy.MaximumDaysPerYear,
			PaidType = policy.PaidType == LeavePaidType.Paid ? "Paid" : "Unpaid",
			CarryForward = policy.CarryForward,
			EncashmentAllowed = policy.Encashable,
			RequiresApproval = policy.RequiresApproval,
			Description = policy.Description,
			CreatedAt = policy.CreationTime
		};
	}

	/// <summary>
	/// Update a leave policy (matches OpenAPI contract)
	/// </summary>
	public async Task<LeavePolicyUpdatedResponse> UpdateLeavePolicyAsync(Guid policyId, LeavePolicyCreateRequest input)
	{
		var policy = await _leavePolicyRepository.GetWithLeaveTypeAsync(policyId);
		
		if (policy == null)
		{
			throw new EntityNotFoundException(typeof(LeavePolicy), policyId);
		}

		var paidType = input.PaidType == "Paid" ? LeavePaidType.Paid : LeavePaidType.Unpaid;

		await _leavePolicyManager.UpdateAsync(
			policy,
			input.PolicyTitle,
			input.LeaveType,
			input.MaxLeavePerYear,
			paidType,
			input.CarryForward,
			input.EncashmentAllowed,
			input.RequiresApproval,
			input.Description
		);

		await Repository.UpdateAsync(policy, autoSave: true);
		await ClearCacheAsync();
		await ClearContractCacheAsync(policy.EducationMediumStageId);

		return new LeavePolicyUpdatedResponse
		{
			Id = policy.Id.ToString(),
			PolicyTitle = policy.PolicyName,
			LeaveType = policy.LeaveTypeId,
			MaxLeavePerYear = policy.MaximumDaysPerYear,
			PaidType = policy.PaidType == LeavePaidType.Paid ? "Paid" : "Unpaid",
			CarryForward = policy.CarryForward,
			EncashmentAllowed = policy.Encashable,
			RequiresApproval = policy.RequiresApproval,
			Description = policy.Description,
			UpdatedAt = DateTime.UtcNow
		};
	}

	/// <summary>
	/// Delete (deactivate) a leave policy (matches OpenAPI contract)
	/// </summary>
	public async Task<LeavePolicyDeleteResponse> DeleteLeavePolicyAsync(Guid policyId)
	{
		// Repository.GetAsync throws EntityNotFoundException if not found
		var policy = await Repository.GetAsync(policyId);

		policy.Deactivate();
		await Repository.UpdateAsync(policy, autoSave: true);
		await ClearCacheAsync();
		await ClearContractCacheAsync(policy.EducationMediumStageId);

		return new LeavePolicyDeleteResponse
		{
			Id = policyId.ToString(),
			Message = "Leave policy deleted successfully",
			DeletedAt = DateTime.UtcNow
		};
	}

	private async Task ClearCacheAsync()
	{
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

	private IQueryable<LeavePolicy> ApplySorting(IQueryable<LeavePolicy> queryable, string sorting)
	{
		if (string.IsNullOrWhiteSpace(sorting))
		{
			return queryable.OrderBy(x => x.PolicyName);
		}

		var sortingParts = sorting.Split(',');
		IOrderedQueryable<LeavePolicy>? orderedQueryable = null;
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
					"policyname" => direction == "DESC" 
						? queryable.OrderByDescending(x => x.PolicyName) 
						: queryable.OrderBy(x => x.PolicyName),
					"maximumdaysperyear" => direction == "DESC"
						? queryable.OrderByDescending(x => x.MaximumDaysPerYear)
						: queryable.OrderBy(x => x.MaximumDaysPerYear),
					"isactive" => direction == "DESC"
						? queryable.OrderByDescending(x => x.IsActive)
						: queryable.OrderBy(x => x.IsActive),
					_ => queryable.OrderBy(x => x.PolicyName)
				};
				isFirstSort = false;
			}
			else
			{
				orderedQueryable = property.ToLower() switch
				{
					"policyname" => direction == "DESC" 
						? orderedQueryable!.ThenByDescending(x => x.PolicyName) 
						: orderedQueryable!.ThenBy(x => x.PolicyName),
					"maximumdaysperyear" => direction == "DESC"
						? orderedQueryable!.ThenByDescending(x => x.MaximumDaysPerYear)
						: orderedQueryable!.ThenBy(x => x.MaximumDaysPerYear),
					"isactive" => direction == "DESC"
						? orderedQueryable!.ThenByDescending(x => x.IsActive)
						: orderedQueryable!.ThenBy(x => x.IsActive),
					_ => orderedQueryable!.ThenBy(x => x.PolicyName)
				};
			}
		}

		return orderedQueryable ?? queryable.OrderBy(x => x.PolicyName);
	}
}

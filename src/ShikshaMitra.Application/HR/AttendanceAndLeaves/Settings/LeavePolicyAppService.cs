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
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

[RemoteService(false)]
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

	private string GetBaseCacheKey() =>
		$"EmployeeLeavePolicies:Active:Tenant:{CurrentTenant?.Id ?? Guid.Empty}";

	private string GetActiveCacheKey(int skipCount, int maxResultCount) =>
		$"{GetBaseCacheKey()}:Skip:{skipCount}:Take:{maxResultCount}";

	private string GetContractCacheKey(Guid academicYearId, int page, int pageSize, string? search) =>
		$"EmployeeLeavePolicies:Contract:Tenant:{CurrentTenant?.Id ?? Guid.Empty}:AcademicYear:{academicYearId}:Page:{page}:Size:{pageSize}:Search:{search ?? string.Empty}";

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

	public override Task<LeavePolicyDto> CreateAsync(CreateLeavePolicyDto input)
	{
		throw new BusinessException(HRErrorCodes.LeavePolicyCreateMethodNotSupported);
	}

	public override Task<LeavePolicyDto> UpdateAsync(Guid id, UpdateLeavePolicyDto input)
	{
		throw new BusinessException(HRErrorCodes.LeavePolicyUpdateMethodNotSupported);
	}

	public override async Task DeleteAsync(Guid id)
	{
		var leavePolicy = await Repository.GetAsync(id);
		leavePolicy.Deactivate();
		await Repository.UpdateAsync(leavePolicy, autoSave: true);
		await ClearCacheAsync();
		await ClearContractCacheAsync(leavePolicy.AcademicYearId);
	}

	public override async Task<PagedResultDto<LeavePolicyDto>> GetListAsync(PagedAndSortedResultRequestDto input)
	{
		var queryable = await Repository.GetQueryableAsync();
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
		var activeCacheKey = GetActiveCacheKey(input.SkipCount, input.MaxResultCount);
		var cached = await _cache.GetAsync(activeCacheKey);
		if (cached != null)
		{
			return cached;
		}

		var queryable = await Repository.GetQueryableAsync();
		var query = queryable.Where(x => x.IsActive);
		var totalCount = await AsyncExecuter.CountAsync(query);
		var items = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.PolicyName).Skip(input.SkipCount).Take(input.MaxResultCount));

		var result = new PagedResultDto<LeavePolicyDto>(
			totalCount,
			ObjectMapper.Map<List<LeavePolicy>, List<LeavePolicyDto>>(items)
		);

		await _cache.SetAsync(
			activeCacheKey,
			result,
			new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) }
		);

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
		Check.Range(pageSize, nameof(pageSize), 1, 100);

		var cacheKey = GetContractCacheKey(academicYearId, page, pageSize, search);
		var cached = await _contractCache.GetAsync(cacheKey, token: cancellationToken);
		if (cached != null)
		{
			return cached;
		}

		var skipCount = (page - 1) * pageSize;
		var items = await _leavePolicyRepository.GetListByAcademicYearIdAsync(
			academicYearId,
			skipCount,
			pageSize,
			sorting: "PolicyName ASC",
			search: search,
			includeInactive: false,
			cancellationToken: cancellationToken);

		var totalCount = await _leavePolicyRepository.GetCountByAcademicYearIdAsync(
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
			Items = items.Select(ToPolicyItem).ToList()
		};

		await _contractCache.SetAsync(
			cacheKey,
			response,
			new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) },
			token: cancellationToken);

		return response;
	}

	public async Task<LeavePolicyResponse> CreateLeavePolicyAsync(Guid academicYearId, LeavePolicyCreateRequest input)
	{
		var paidType = ParsePaidType(input.PaidType);
		var accrualPolicyType = ParseAccrualPolicyType(input.AccrualPolicyType);
		var encashmentTiming = ParseEncashmentTiming(input.EncashmentTiming);

		if (input.EncashmentAllowed && !encashmentTiming.HasValue)
		{
			throw new BusinessException("ShikshaMitra:EncashmentTimingRequired");
		}

		var policy = await _leavePolicyManager.CreateAsync(
			academicYearId,
			input.PolicyTitle,
			input.LeaveType,
			accrualPolicyType,
			input.LeaveCount,
			paidType,
			input.CarryForward,
			input.EncashmentAllowed,
			encashmentTiming,
			input.RequiresApproval,
			input.Description);

		await Repository.InsertAsync(policy, autoSave: true);
		await ClearCacheAsync();
		await ClearContractCacheAsync(academicYearId);

		return new LeavePolicyResponse
		{
			Id = policy.Id,
			PolicyTitle = policy.PolicyName,
			LeaveType = policy.LeaveTypeId,
			AccrualPolicyType = ToAccrualPolicyType(policy.AccrualPolicyType),
			LeaveCount = policy.LeaveCount,
			PaidType = ToPaidType(policy.PaidType),
			CarryForward = policy.CarryForward,
			EncashmentAllowed = policy.Encashable,
			EncashmentTiming = ToEncashmentTiming(policy.EncashmentTiming),
			RequiresApproval = policy.RequiresApproval,
			Description = policy.Description,
			CreatedAt = policy.CreationTime
		};
	}

	public async Task<LeavePolicyDetailResponse> GetLeavePolicyByIdAsync(Guid policyId)
	{
		var policy = await _leavePolicyRepository.GetWithLeaveTypeAsync(policyId);
		if (policy == null)
		{
			throw new EntityNotFoundException(typeof(LeavePolicy), policyId);
		}

		return ToDetail(policy);
	}

	public async Task<LeavePolicyDetailResponse> UpdateLeavePolicyAsync(Guid policyId, LeavePolicyUpdateRequest input)
	{
		var policy = await _leavePolicyRepository.GetWithLeaveTypeAsync(policyId);
		if (policy == null)
		{
			throw new EntityNotFoundException(typeof(LeavePolicy), policyId);
		}

		var paidType = ParsePaidType(input.PaidType);
		var accrualPolicyType = ParseAccrualPolicyType(input.AccrualPolicyType);
		var encashmentTiming = ParseEncashmentTiming(input.EncashmentTiming);

		if (input.EncashmentAllowed && !encashmentTiming.HasValue)
		{
			throw new BusinessException("ShikshaMitra:EncashmentTimingRequired");
		}

		await _leavePolicyManager.UpdateAsync(
			policy,
			input.PolicyTitle,
			input.LeaveType,
			accrualPolicyType,
			input.LeaveCount,
			paidType,
			input.CarryForward,
			input.EncashmentAllowed,
			encashmentTiming,
			input.RequiresApproval,
			input.Description);

		await Repository.UpdateAsync(policy, autoSave: true);
		await ClearCacheAsync();
		await ClearContractCacheAsync(policy.AcademicYearId);

		policy = await _leavePolicyRepository.GetWithLeaveTypeAsync(policyId) ?? policy;
		return ToDetail(policy);
	}

	public async Task<LeavePolicyDeleteResponse> DeleteLeavePolicyAsync(Guid policyId)
	{
		var policy = await Repository.GetAsync(policyId);
		policy.Deactivate();
		await Repository.UpdateAsync(policy, autoSave: true);
		await ClearCacheAsync();
		await ClearContractCacheAsync(policy.AcademicYearId);

		return new LeavePolicyDeleteResponse
		{
			Id = policyId.ToString(),
			Message = "Leave policy deleted successfully",
			DeletedAt = DateTime.UtcNow
		};
	}

	private static LeavePolicyItem ToPolicyItem(LeavePolicy policy) => new()
	{
		Id = policy.Id,
		PolicyTitle = policy.PolicyName,
		LeaveType = new LeaveTypeReference
		{
			Id = policy.LeaveTypeId,
			Name = policy.LeaveType?.Type ?? string.Empty
		},
		AccrualPolicyType = ToAccrualPolicyType(policy.AccrualPolicyType),
		LeaveCount = policy.LeaveCount,
		CarryForward = policy.CarryForward,
		EncashmentAllowed = policy.Encashable,
		EncashmentTiming = ToEncashmentTiming(policy.EncashmentTiming),
		PaidType = ToPaidType(policy.PaidType),
		RequiresApproval = policy.RequiresApproval,
		Description = policy.Description
	};

	private static LeavePolicyDetailResponse ToDetail(LeavePolicy policy) => new()
	{
		Id = policy.Id,
		PolicyTitle = policy.PolicyName,
		LeaveType = new LeaveTypeReference
		{
			Id = policy.LeaveTypeId,
			Name = policy.LeaveType?.Type ?? string.Empty
		},
		AccrualPolicyType = ToAccrualPolicyType(policy.AccrualPolicyType),
		LeaveCount = policy.LeaveCount,
		PaidType = ToPaidType(policy.PaidType),
		CarryForward = policy.CarryForward,
		EncashmentAllowed = policy.Encashable,
		EncashmentTiming = ToEncashmentTiming(policy.EncashmentTiming),
		RequiresApproval = policy.RequiresApproval,
		Description = policy.Description
	};

	private static LeavePaidType ParsePaidType(string paidType) =>
		string.Equals(paidType, "Paid", StringComparison.OrdinalIgnoreCase)
			? LeavePaidType.Paid
			: LeavePaidType.Unpaid;

	private static string ToPaidType(LeavePaidType paidType) => paidType == LeavePaidType.Paid ? "Paid" : "Unpaid";

	private static LeaveAccrualPolicyType ParseAccrualPolicyType(string value) =>
		string.Equals(value, "Yearly", StringComparison.OrdinalIgnoreCase)
			? LeaveAccrualPolicyType.Yearly
			: LeaveAccrualPolicyType.Monthly;

	private static string ToAccrualPolicyType(LeaveAccrualPolicyType value) =>
		value == LeaveAccrualPolicyType.Yearly ? "Yearly" : "Monthly";

	private static LeaveEncashmentTiming? ParseEncashmentTiming(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		return value switch
		{
			"next_payroll" => LeaveEncashmentTiming.NextPayroll,
			"end_of_year" => LeaveEncashmentTiming.EndOfYear,
			_ => null
		};
	}

	private static string? ToEncashmentTiming(LeaveEncashmentTiming? value) => value switch
	{
		LeaveEncashmentTiming.NextPayroll => "next_payroll",
		LeaveEncashmentTiming.EndOfYear => "end_of_year",
		_ => null
	};

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

	private IQueryable<LeavePolicy> ApplySorting(IQueryable<LeavePolicy> queryable, string sorting)
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
					"leavecount" => direction == "DESC" ? queryable.OrderByDescending(x => x.LeaveCount) : queryable.OrderBy(x => x.LeaveCount),
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
					"leavecount" => direction == "DESC" ? orderedQueryable!.ThenByDescending(x => x.LeaveCount) : orderedQueryable!.ThenBy(x => x.LeaveCount),
					"isactive" => direction == "DESC" ? orderedQueryable!.ThenByDescending(x => x.IsActive) : orderedQueryable!.ThenBy(x => x.IsActive),
					_ => orderedQueryable!.ThenBy(x => x.PolicyName)
				};
			}
		}

		return orderedQueryable ?? queryable.OrderBy(x => x.PolicyName);
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings.Managers;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using Volo.Abp;
using Volo.Abp.Uow;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.Settings;

[Authorize]
[RemoteService(false)]
public class FeeCategoryAppService : ApplicationService, IFeeCategoryAppService
{
	private readonly IRepository<FeeCategory, Guid> feeCategoryRepository;
	private readonly IRepository<FeeHead, Guid> feeHeadRepository;
	private readonly IRepository<TransactionCategory, Guid> incomeCategoryRepository;
	private readonly IRepository<AccountHead, Guid> accountHeadRepository;
	private readonly FeeCategoryManager feeCategoryManager;
	private readonly FeeHeadAccountingManager accountingManager;
	private readonly FeeHeadDependencyQuery dependencyQuery;

	public FeeCategoryAppService(
		IRepository<FeeCategory, Guid> feeCategoryRepository,
		IRepository<FeeHead, Guid> feeHeadRepository,
		IRepository<FeeStructure, Guid> feeStructureRepository,
		IRepository<TransactionCategory, Guid> incomeCategoryRepository,
		IRepository<AccountHead, Guid> accountHeadRepository,
		FeeCategoryManager feeCategoryManager,
		FeeHeadAccountingManager accountingManager,
		FeeHeadDependencyQuery dependencyQuery
	)
	{
		this.feeCategoryRepository = feeCategoryRepository;
		this.feeHeadRepository = feeHeadRepository;
		_ = feeStructureRepository;
		this.incomeCategoryRepository = incomeCategoryRepository;
		this.accountHeadRepository = accountHeadRepository;
		this.feeCategoryManager = feeCategoryManager;
		this.accountingManager = accountingManager;
		this.dependencyQuery = dependencyQuery;
	}

	[UnitOfWork(isTransactional: true)]
	public async Task<FeeCategoryResponse> CreateAsync(Guid academicYearId, FeeCategoryCreateRequest input)
	{
		await feeCategoryManager.EnsureNameIsUniqueAsync(academicYearId, input.Name);

		var feeCategory = new FeeCategory(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			input.Name.Trim(),
			input.Description
		);

		await feeCategoryRepository.InsertAsync(feeCategory);

		var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var feeHeads = new List<FeeHead>();

		foreach (var headInput in input.FeeHeads)
		{
			await accountingManager.GetValidMappingAsync(academicYearId, headInput.IncomeCategoryId, creatingReference: true);
			var normalizedName = headInput.Name.Trim();
			if (!seenNames.Add(normalizedName))
			{
				throw new BusinessException(FinanceFeeCategoryErrorCodes.DuplicateFeeHeadName)
					.WithData("Name", normalizedName);
			}

			var feeHead = new FeeHead(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				academicYearId,
				feeCategory.Id,
				normalizedName,
				null, // Retired for newly created fee heads.
				headInput.IsConcessionApplied,
				headInput.Description,
				headInput.IncomeCategoryId
			);

			feeHeads.Add(feeHead);
		}

		foreach (var feeHead in feeHeads)
		{
			await feeHeadRepository.InsertAsync(feeHead);
		}

		return await MapToResponseAsync(feeCategory, feeHeads);
	}

	public async Task<FeeCategoryListResponse> GetListAsync(Guid academicYearId, string? search = null, int page = 1, int pageSize = 10, bool? isActive = null, string? filters = null, string? sortBy = null, string? sortDirection = null)
	{
		page = Math.Max(1, page);
		pageSize = Math.Clamp(pageSize, 1, 100);
		var query = await feeCategoryRepository.GetQueryableAsync();
		var headQuery = await feeHeadRepository.GetQueryableAsync();

		query = query.Where(x => x.AcademicYearId == academicYearId);

		if (isActive.HasValue)
		{
			query = query.Where(x => x.IsActive == isActive.Value);
		}

		if (!string.IsNullOrWhiteSpace(search))
		{
			var normalizedSearch = search.Trim().ToLower();
			query = query.Where(x => x.Name.ToLower().Contains(normalizedSearch));
		}

		if (!string.IsNullOrWhiteSpace(filters))
		{
			FeeCategoryTableFilter[] requestedFilters;
			try
			{
				requestedFilters = JsonSerializer.Deserialize<FeeCategoryTableFilter[]>(filters,
					new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
			}
			catch (JsonException)
			{
				throw new UserFriendlyException("Invalid fee category filters.");
			}
			if (requestedFilters.Length > 20)
				throw new UserFriendlyException("Too many fee category filters.");
			foreach (var filter in requestedFilters)
			{
				if (filter.Column is "name" or "description")
				{
					if (filter.Value.ValueKind != JsonValueKind.String || filter.Type is not ("contains" or "equals" or "startsWith" or "endsWith"))
						throw new UserFriendlyException("Invalid fee category text filter.");
					var value = (filter.Value.GetString() ?? string.Empty).Trim().ToLower();
					if (value.Length > 200)
						throw new UserFriendlyException("Fee category filter text is too long.");
					if (filter.Column == "name")
						query = filter.Type switch
						{
							"equals" => query.Where(x => x.Name.ToLower() == value),
							"startsWith" => query.Where(x => x.Name.ToLower().StartsWith(value)),
							"endsWith" => query.Where(x => x.Name.ToLower().EndsWith(value)),
							_ => query.Where(x => x.Name.ToLower().Contains(value))
						};
					else
						query = filter.Type switch
						{
							"equals" => query.Where(x => (x.Description ?? "").ToLower() == value),
							"startsWith" => query.Where(x => (x.Description ?? "").ToLower().StartsWith(value)),
							"endsWith" => query.Where(x => (x.Description ?? "").ToLower().EndsWith(value)),
							_ => query.Where(x => (x.Description ?? "").ToLower().Contains(value))
						};
				}
				else if (filter.Column == "totalFeeHeads")
				{
					if (filter.Type == "between" && filter.Value.ValueKind == JsonValueKind.Object &&
						filter.Value.TryGetProperty("min", out var minValue) && minValue.TryGetInt32(out var min) &&
						filter.Value.TryGetProperty("max", out var maxValue) && maxValue.TryGetInt32(out var max) && min <= max)
						query = query.Where(category => headQuery.Count(h => h.FeeCategoryId == category.Id) >= min && headQuery.Count(h => h.FeeCategoryId == category.Id) <= max);
					else if (filter.Value.ValueKind == JsonValueKind.Number && filter.Value.TryGetInt32(out var number) && number >= 0)
						query = filter.Type switch
						{
							"equals" => query.Where(category => headQuery.Count(h => h.FeeCategoryId == category.Id) == number),
							"greaterThan" => query.Where(category => headQuery.Count(h => h.FeeCategoryId == category.Id) > number),
							"lessThan" => query.Where(category => headQuery.Count(h => h.FeeCategoryId == category.Id) < number),
							_ => throw new UserFriendlyException("Invalid fee head count filter.")
						};
					else
						throw new UserFriendlyException("Invalid fee head count filter.");
				}
				else if (filter.Column == "isActive")
				{
					if (filter.Type != "equals")
						throw new UserFriendlyException("Invalid fee category status filter.");
					bool? expectedStatus = filter.Value.ValueKind switch
					{
						JsonValueKind.True => true,
						JsonValueKind.False => false,
						JsonValueKind.String when bool.TryParse(filter.Value.GetString(), out var parsed) => parsed,
						_ => null
					};
					if (!expectedStatus.HasValue)
						throw new UserFriendlyException("Invalid fee category status filter.");
					query = query.Where(x => x.IsActive == expectedStatus.Value);
				}
				else
					throw new UserFriendlyException("Unsupported fee category filter.");
			}
		}

		var totalCount = await feeCategoryRepository.AsyncExecuter.CountAsync(query);

		var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
		query = sortBy switch
		{
			"name" => descending ? query.OrderByDescending(x => x.Name).ThenBy(x => x.Id) : query.OrderBy(x => x.Name).ThenBy(x => x.Id),
			"description" => descending ? query.OrderByDescending(x => x.Description).ThenBy(x => x.Id) : query.OrderBy(x => x.Description).ThenBy(x => x.Id),
			"totalFeeHeads" => descending ? query.OrderByDescending(x => headQuery.Count(h => h.FeeCategoryId == x.Id)).ThenBy(x => x.Id) : query.OrderBy(x => headQuery.Count(h => h.FeeCategoryId == x.Id)).ThenBy(x => x.Id),
			_ => query.OrderByDescending(x => x.CreationTime).ThenBy(x => x.Id)
		};
		query = query
					 .Skip((page - 1) * pageSize)
					 .Take(pageSize);

		var categories = await feeCategoryRepository.AsyncExecuter.ToListAsync(query);
		var categoryIds = categories.Select(x => x.Id).ToList();

		var allHeads = await feeHeadRepository.AsyncExecuter.ToListAsync(
			headQuery.Where(x => categoryIds.Contains(x.FeeCategoryId))
		);

		var usedFeeHeadIds = await GetUsedFeeHeadIdsAsync(allHeads.Select(x => x.Id).ToList());

		var items = new List<FeeCategoryResponse>();
		foreach (var category in categories)
		{
			var heads = allHeads.Where(x => x.FeeCategoryId == category.Id).ToList();
			items.Add(await MapToResponseAsync(category, heads, usedFeeHeadIds));
		}

		return new FeeCategoryListResponse
		{
			TotalCount = totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
			Items = items
		};
	}

	private sealed record FeeCategoryTableFilter(string Column, string Type, JsonElement Value);

	public async Task<FeeCategoryResponse> GetByIdAsync(Guid id)
	{
		var feeCategory = await feeCategoryRepository.GetAsync(id);
		var feeHeads = await feeHeadRepository.GetListAsync(x => x.FeeCategoryId == id);

		return await MapToResponseAsync(feeCategory, feeHeads);
	}

	[UnitOfWork(isTransactional: true)]
	public async Task<FeeCategoryResponse> UpdateAsync(Guid id, FeeCategoryUpdateRequest input)
	{
		var feeCategory = await feeCategoryRepository.GetAsync(id);

		await feeCategoryManager.EnsureNameIsUniqueAsync(feeCategory.AcademicYearId, input.Name, id);



		var existingHeads = await feeHeadRepository.GetListAsync(x => x.FeeCategoryId == id);
		var existingHeadsById = existingHeads.ToDictionary(x => x.Id);
		var unknownHeadIds = input.FeeHeads
			.Where(x => x.Id.HasValue && !existingHeadsById.ContainsKey(x.Id.Value))
			.Select(x => x.Id!.Value)
			.ToList();
		if (unknownHeadIds.Count > 0)
		{
			throw new BusinessException(FinanceFeeCategoryErrorCodes.InvalidFeeHeadReference)
				.WithData("Ids", string.Join(",", unknownHeadIds));
		}
		var incomingIds = input.FeeHeads.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToHashSet();
		var usedFeeHeadIds = await GetUsedFeeHeadIdsAsync(existingHeads.Select(x => x.Id).ToList());
		foreach (var removed in existingHeads.Where(x => !incomingIds.Contains(x.Id)))
		{
			var dependencies = (await dependencyQuery.GetAsync([removed.Id]))[removed.Id];
			if (dependencies.IsBlocked)
				throw FeeHeadDependencyQuery.Blocked(FinanceFeeHeadErrorCodes.CannotDeleteInUse, removed.Name, dependencies);
		}
		feeCategory.Update(input.Name.Trim(), input.Description);
		await feeCategoryRepository.UpdateAsync(feeCategory);

		var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var resultHeads = new List<FeeHead>();

		foreach (var headInput in input.FeeHeads)
		{
			await accountingManager.GetValidMappingAsync(feeCategory.AcademicYearId, headInput.IncomeCategoryId, creatingReference: true);
			var normalizedName = headInput.Name.Trim();
			if (!seenNames.Add(normalizedName))
			{
				throw new BusinessException(FinanceFeeCategoryErrorCodes.DuplicateFeeHeadName)
					.WithData("Name", normalizedName);
			}

			if (headInput.Id.HasValue && existingHeadsById.TryGetValue(headInput.Id.Value, out var existingHead))
			{
				if (existingHead.IncomeCategoryId != headInput.IncomeCategoryId && usedFeeHeadIds.Contains(existingHead.Id))
				{
					throw new BusinessException(FinanceFeeHeadErrorCodes.IncomeCategoryChangeBlocked);
				}
				await feeCategoryManager.EnsureFeeHeadNameIsUniqueAsync(
					feeCategory.AcademicYearId, id, normalizedName, existingHead.Id);

				existingHead.Update(id, normalizedName, existingHead.Module, headInput.IsConcessionApplied,
					headInput.Description, headInput.IncomeCategoryId);
				await feeHeadRepository.UpdateAsync(existingHead);
				resultHeads.Add(existingHead);
			}
			else
			{
				await feeCategoryManager.EnsureFeeHeadNameIsUniqueAsync(feeCategory.AcademicYearId, id, normalizedName);

				var newHead = new FeeHead(
					GuidGenerator.Create(),
					CurrentTenant.Id,
					feeCategory.AcademicYearId,
					id,
					normalizedName,
					null, // Retired for newly created fee heads.
					headInput.IsConcessionApplied,
					headInput.Description,
					headInput.IncomeCategoryId
				);
				await feeHeadRepository.InsertAsync(newHead);
				resultHeads.Add(newHead);
			}
		}

		var removedHeads = existingHeads.Where(x => !incomingIds.Contains(x.Id)).ToList();
		if (removedHeads.Count > 0)
		{
			var removalDependencies = await dependencyQuery.GetAsync(removedHeads.Select(x => x.Id));
			foreach (var removedHead in removedHeads)
			{
				if (removalDependencies[removedHead.Id].IsBlocked)
					throw FeeHeadDependencyQuery.Blocked(FinanceFeeHeadErrorCodes.CannotDeleteInUse, removedHead.Name, removalDependencies[removedHead.Id]);
				await feeHeadRepository.DeleteAsync(removedHead);
			}
		}

		return await MapToResponseAsync(feeCategory, resultHeads);
	}

	public async Task<MessageResponse> DeleteAsync(Guid id)
	{
		var feeHeads = await feeHeadRepository.GetListAsync(x => x.FeeCategoryId == id);
		var usedIds = await GetUsedFeeHeadIdsAsync(feeHeads.Select(x => x.Id).ToList());

		if (usedIds.Count > 0)
		{
			throw new BusinessException(FinanceFeeCategoryErrorCodes.CannotDeleteInUse);
		}

		foreach (var feeHead in feeHeads)
		{
			await feeHeadRepository.DeleteAsync(feeHead);
		}

		await feeCategoryRepository.DeleteAsync(id);

		return new MessageResponse
		{
			Message = "Fee category deleted successfully"
		};
	}

	public async Task<FeeCategoryResponse> UpdateStatusAsync(Guid id, FeeCategoryStatusUpdateRequest input)
	{
		var feeCategory = await feeCategoryRepository.GetAsync(id);
		var feeHeads = await feeHeadRepository.GetListAsync(x => x.FeeCategoryId == id);

		if (!input.IsActive)
		{
			var dependencies = await dependencyQuery.GetAsync(feeHeads.Select(x => x.Id));
			foreach (var head in feeHeads.Where(x => dependencies[x.Id].IsBlocked))
				throw FeeHeadDependencyQuery.Blocked(FinanceFeeHeadErrorCodes.CannotDeactivateInUse, head.Name, dependencies[head.Id]);
		}

		feeCategory.SetStatus(input.IsActive, input.DeactivationReason);
		await feeCategoryRepository.UpdateAsync(feeCategory);

		foreach (var feeHead in feeHeads)
		{
			if (input.IsActive)
			{
				await accountingManager.GetValidMappingAsync(feeHead.AcademicYearId, feeHead.IncomeCategoryId);
			}
			feeHead.SetActiveStatus(input.IsActive, input.DeactivationReason);
			await feeHeadRepository.UpdateAsync(feeHead);
		}

		return await MapToResponseAsync(feeCategory, feeHeads);
	}

	private async Task<HashSet<Guid>> GetUsedFeeHeadIdsAsync(List<Guid> feeHeadIds)
	{
		if (feeHeadIds.Count == 0)
		{
			return new HashSet<Guid>();
		}

		return (await dependencyQuery.GetAsync(feeHeadIds)).Where(x => x.Value.IsBlocked).Select(x => x.Key).ToHashSet();
	}

	private async Task<FeeCategoryResponse> MapToResponseAsync(FeeCategory feeCategory, List<FeeHead> feeHeads, HashSet<Guid>? usedFeeHeadIds = null)
	{
		usedFeeHeadIds ??= await GetUsedFeeHeadIdsAsync(feeHeads.Select(x => x.Id).ToList());
		var incomeCategoryIds = feeHeads.Select(x => x.IncomeCategoryId).Distinct().ToList();
		var incomeCategories = await incomeCategoryRepository.GetListAsync(x => incomeCategoryIds.Contains(x.Id));
		var incomeCategoryDict = incomeCategories.ToDictionary(x => x.Id);
		var accountHeadIds = incomeCategories.Where(x => x.DefaultAccountHeadId.HasValue)
			.Select(x => x.DefaultAccountHeadId!.Value).Distinct().ToList();
		var accountHeads = await accountHeadRepository.GetListAsync(x => accountHeadIds.Contains(x.Id));
		var accountHeadDict = accountHeads.ToDictionary(x => x.Id);

		var dependencies = await dependencyQuery.GetAsync(feeHeads.Select(x => x.Id));
		var response = ObjectMapper.Map<FeeCategory, FeeCategoryResponse>(feeCategory);
		response.FeeHeads = feeHeads.Select(feeHead =>
		{
			var headResponse = ObjectMapper.Map<FeeHead, FeeHeadResponse>(feeHead);
			headResponse.FeeCategoryName = feeCategory.Name;
			if (incomeCategoryDict.TryGetValue(feeHead.IncomeCategoryId, out var incomeCategory))
			{
				headResponse.IncomeCategoryName = incomeCategory.Name;
				if (incomeCategory.DefaultAccountHeadId.HasValue &&
					accountHeadDict.TryGetValue(incomeCategory.DefaultAccountHeadId.Value, out var accountHead))
				{
					headResponse.IncomeAccountHeadId = accountHead.Id;
					headResponse.IncomeAccountHeadName = accountHead.Name;
				}
			}
			headResponse.Dependencies = dependencies[feeHead.Id];
			headResponse.IsUsedInDownstream = headResponse.Dependencies.IsBlocked;
			return headResponse;
		}).ToList();
		response.IsUsedInDownstream = feeHeads.Any(x => usedFeeHeadIds.Contains(x.Id));

		return response;
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings.Managers;
using ShikshaMitra.Finance.Cache;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.Settings;

[Authorize]
[RemoteService(false)]
public class FeeCategoryAppService(
	IRepository<FeeCategory, Guid> feeCategoryRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IAcademicYearRepository academicYearRepository,
	FeeCategoryManager feeCategoryManager,
	FeeHeadUsageService usageService,
	FinanceReferenceCacheService referenceCache
) : ApplicationService, IFeeCategoryAppService
{
	public async Task<FeeCategoryResponse> CreateAsync(Guid academicYearId, FeeCategoryCreateRequest input)
	{
		await academicYearRepository.GetAsync(academicYearId);
		await feeCategoryManager.EnsureNameIsUniqueAsync(academicYearId, input.Name);
		var category = new FeeCategory(GuidGenerator.Create(), CurrentTenant.Id, academicYearId, input.Name, Clean(input.Description));
		await feeCategoryRepository.InsertAsync(category);
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var heads = new List<FeeHead>();
		foreach (var item in input.FeeHeads)
		{
			if (!seen.Add(FeeCategoryManager.NormalizeName(item.Name)))
				throw new BusinessException(FinanceFeeCategoryErrorCodes.DuplicateFeeHeadName)
					.WithData("Name", item.Name)
					.WithData("Field", $"FeeHeads[{heads.Count}].Name");
			var head = new FeeHead(GuidGenerator.Create(), CurrentTenant.Id, academicYearId, category.Id,
				item.Name, item.Module, item.IsConcessionApplied, Clean(item.Description));
			await feeHeadRepository.InsertAsync(head);
			heads.Add(head);
		}
		try
		{
			await CurrentUnitOfWork.SaveChangesAsync();
		}
		catch (Exception exception) when (FeeSetupPersistenceErrors.IsCategoryNameConflict(exception))
		{
			throw new BusinessException(FinanceFeeCategoryErrorCodes.DuplicateName).WithData("Name", input.Name);
		}
		await referenceCache.InvalidateFeeHeadsAsync();
		return await MapAsync(category, heads);
	}

	public async Task<FeeCategoryListResponse> GetListAsync(Guid academicYearId, string? search = null, string categoryStatus = "all", string sort = "name", int page = 1, int pageSize = 10)
	{
		ValidatePage(page, pageSize);
		var allowedStatuses = new[] { "all", "active", "inactive", "used", "incomplete" };
		var allowedSorts = new[] { "name", "name-desc", "heads-desc" };
		if (!allowedStatuses.Contains(categoryStatus, StringComparer.OrdinalIgnoreCase) ||
			!allowedSorts.Contains(sort, StringComparer.OrdinalIgnoreCase))
			throw new BusinessException(FinanceFeeCategoryErrorCodes.InvalidQuery)
				.WithData("CategoryStatus", categoryStatus)
				.WithData("Sort", sort);
		await academicYearRepository.GetAsync(academicYearId);
		var allCategories = await feeCategoryRepository.GetListAsync(x => x.AcademicYearId == academicYearId);
		var allHeads = await feeHeadRepository.GetListAsync(x => x.AcademicYearId == academicYearId);
		var usage = await usageService.GetUsageAsync(allHeads.Select(x => x.Id));
		var headsByCategory = allHeads.GroupBy(x => x.FeeCategoryId).ToDictionary(x => x.Key, x => x.ToList());
		var usedHeadIds = usage.Where(x => x.Value.Count > 0).Select(x => x.Key).ToHashSet();
		var usedCategoryIds = allHeads.Where(x => usedHeadIds.Contains(x.Id)).Select(x => x.FeeCategoryId).ToHashSet();
		var currentHeadCounts = allHeads.Where(x => x.IsCurrentRevision).GroupBy(x => x.FeeCategoryId)
			.ToDictionary(x => x.Key, x => x.Count());
		IEnumerable<FeeCategory> filtered = allCategories;
		if (!string.IsNullOrWhiteSpace(search))
		{
			var normalized = FeeCategoryManager.NormalizeName(search);
			var matchingCategoryIds = allHeads.Where(x => SearchText(x.Name, x.Description).Contains(normalized))
				.Select(x => x.FeeCategoryId)
				.ToHashSet();
			filtered = filtered.Where(x => SearchText(x.Name, x.Description).Contains(normalized) || matchingCategoryIds.Contains(x.Id));
		}
		filtered = categoryStatus.ToLowerInvariant() switch
		{
			"active" => filtered.Where(x => x.IsActive),
			"inactive" => filtered.Where(x => !x.IsActive),
			"used" => filtered.Where(x => usedCategoryIds.Contains(x.Id)),
			"incomplete" => filtered.Where(x => currentHeadCounts.GetValueOrDefault(x.Id) == 0),
			_ => filtered
		};
		filtered = sort.ToLowerInvariant() switch
		{
			"name-desc" => filtered.OrderByDescending(x => x.Name).ThenBy(x => x.Id),
			"heads-desc" => filtered.OrderByDescending(x => currentHeadCounts.GetValueOrDefault(x.Id))
				.ThenBy(x => x.Name).ThenBy(x => x.Id),
			_ => filtered.OrderBy(x => x.Name).ThenBy(x => x.Id)
		};
		var totalCount = filtered.Count();
		var categories = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
		var currentHeads = allHeads.Where(x => x.IsCurrentRevision).ToList();
		return new FeeCategoryListResponse
		{
			TotalCount = totalCount, CurrentPage = page, PageSize = pageSize,
			TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
			Items = categories.Select(x => Map(x, headsByCategory.GetValueOrDefault(x.Id) ?? [], usage)).ToList(),
			Summary = new FeeSetupSummaryResponse
			{
				CategoryCount = allCategories.Count,
				FeeHeadCount = currentHeads.Count,
				ActiveRecordCount = allCategories.Count(x => x.IsActive) + currentHeads.Count(x => x.IsActive && x.FeeCategory.IsActive),
				InactiveRecordCount = allCategories.Count(x => !x.IsActive) + currentHeads.Count(x => !x.IsActive || !x.FeeCategory.IsActive),
				InUseRecordCount = usedCategoryIds.Count + usedHeadIds.Count
			}
		};
	}

	public async Task<FeeCategoryResponse> GetByIdAsync(Guid id)
	{
		var category = await feeCategoryRepository.GetAsync(id);
		return await MapAsync(category, await feeHeadRepository.GetListAsync(x => x.FeeCategoryId == id));
	}

	public async Task<FeeCategoryResponse> UpdateAsync(Guid id, FeeCategoryUpdateRequest input)
	{
		var category = await feeCategoryRepository.GetAsync(id);
		EnsureConcurrency(category.ConcurrencyStamp, input.ConcurrencyStamp, FinanceFeeCategoryErrorCodes.ConcurrencyConflict);
		await feeCategoryManager.EnsureNameIsUniqueAsync(category.AcademicYearId, input.Name, id);
		category.Update(input.Name, Clean(input.Description));
		await feeCategoryRepository.UpdateAsync(category);
		try
		{
			await CurrentUnitOfWork.SaveChangesAsync();
		}
		catch (Exception exception) when (FeeSetupPersistenceErrors.IsCategoryNameConflict(exception))
		{
			throw new BusinessException(FinanceFeeCategoryErrorCodes.DuplicateName).WithData("Name", input.Name);
		}
		return await MapAsync(category, await feeHeadRepository.GetListAsync(x => x.FeeCategoryId == id));
	}

	public async Task<MessageResponse> DeleteAsync(Guid id)
	{
		await feeCategoryRepository.GetAsync(id);
		var heads = await feeHeadRepository.GetListAsync(x => x.FeeCategoryId == id);
		var usage = await usageService.GetUsageAsync(heads.Select(x => x.Id));
		var blockers = usage.Values.SelectMany(x => x).Distinct().ToList();
		if (blockers.Count > 0)
			throw new BusinessException(FinanceFeeCategoryErrorCodes.CannotDeleteInUse).WithData("Blockers", blockers);
		foreach (var head in heads) await feeHeadRepository.DeleteAsync(head);
		await feeCategoryRepository.DeleteAsync(id);
		await referenceCache.InvalidateFeeHeadsAsync();
		return new MessageResponse { Message = "Fee category deleted successfully" };
	}

	public async Task<FeeCategoryResponse> UpdateStatusAsync(Guid id, FeeCategoryStatusUpdateRequest input)
	{
		var category = await feeCategoryRepository.GetAsync(id);
		EnsureConcurrency(category.ConcurrencyStamp, input.ConcurrencyStamp, FinanceFeeCategoryErrorCodes.ConcurrencyConflict);
		if (input.IsActive && !category.IsActive &&
			!await feeHeadRepository.AnyAsync(x => x.FeeCategoryId == id && x.IsCurrentRevision))
			throw new BusinessException(FinanceFeeCategoryErrorCodes.ActiveCategoryRequiresFeeHead);
		category.SetStatus(input.IsActive, Clean(input.DeactivationReason));
		await feeCategoryRepository.UpdateAsync(category, autoSave: true);
		return await MapAsync(category, await feeHeadRepository.GetListAsync(x => x.FeeCategoryId == id));
	}

	public async Task<FeeCategoryCopyPreviewResponse> PreviewCopyAsync(FeeCategoryCopyPreviewRequest input)
	{
		if (input.SourceAcademicYearId == input.DestinationAcademicYearId)
			throw new BusinessException(FinanceFeeCategoryErrorCodes.CopyConflict).WithData("Reason", "Source and destination years must differ.");
		await academicYearRepository.GetAsync(input.SourceAcademicYearId);
		await academicYearRepository.GetAsync(input.DestinationAcademicYearId);
		var source = await feeCategoryRepository.GetListAsync(x => x.AcademicYearId == input.SourceAcademicYearId &&
			(input.CategoryIds.Count == 0 || input.CategoryIds.Contains(x.Id)));
		if (input.CategoryIds.Count > 0 && source.Count != input.CategoryIds.Distinct().Count())
			throw new BusinessException(FinanceFeeCategoryErrorCodes.CopyConflict).WithData("Reason", "One or more source categories were not found in the source academic year.");
		var destination = await feeCategoryRepository.GetListAsync(x => x.AcademicYearId == input.DestinationAcademicYearId);
		var sourceHeads = await feeHeadRepository.GetListAsync(x => x.AcademicYearId == input.SourceAcademicYearId && x.IsCurrentRevision);
		var destinationHeads = await feeHeadRepository.GetListAsync(x => x.AcademicYearId == input.DestinationAcademicYearId && x.IsCurrentRevision);
		return new FeeCategoryCopyPreviewResponse
		{
			Items = source.Select(category =>
			{
				var match = destination.FirstOrDefault(x => x.NormalizedName == category.NormalizedName);
				var heads = sourceHeads.Where(x => x.FeeCategoryId == category.Id).ToList();
				var matchingHeads = match == null
					? new List<FeeHead>()
					: destinationHeads.Where(x => x.FeeCategoryId == match.Id).ToList();
				var identical = match != null && category.Description == match.Description && category.IsActive == match.IsActive &&
					HeadsMatch(heads, matchingHeads);
				var classification = match == null ? "New" : identical ? "Identical" : "Conflict";
				return new FeeCategoryCopyPreviewItem
				{
					SourceCategoryId = category.Id,
					Name = category.Name,
					Classification = classification,
					FeeHeadCount = heads.Count,
					IsActive = category.IsActive,
					FeeHeads = heads.OrderBy(x => x.Name).Select(head =>
					{
						var destinationHead = matchingHeads.FirstOrDefault(x => x.NormalizedName == head.NormalizedName);
						var headClassification = match == null || destinationHead == null
							? "New"
							: HeadMatches(head, destinationHead) ? "Identical" : "Conflict";
						return new FeeHeadCopyPreviewItem
						{
							SourceFeeHeadId = head.Id,
							Name = head.Name,
							Description = head.Description,
							Module = head.Module.ToString(),
							IsConcessionApplied = head.IsConcessionApplied,
							IsActive = head.IsActive,
							Classification = headClassification
						};
					}).ToList()
				};
			}).ToList()
		};
	}

	public async Task<FeeCategoryCopyResult> CopyAsync(FeeCategoryCopyCommitRequest input)
	{
		var preview = await PreviewCopyAsync(input);
		if (input.Resolutions.Any(x => preview.Items.All(item => item.SourceCategoryId != x.SourceCategoryId)))
			throw new BusinessException(FinanceFeeCategoryErrorCodes.CopyConflict).WithData("Reason", "A copy resolution references an unknown source category.");
		var resolutions = input.Resolutions.ToDictionary(x => x.SourceCategoryId);
		var selectedIds = preview.Items.Select(i => i.SourceCategoryId).ToList();
		var sourceCategories = await feeCategoryRepository.GetListAsync(x => x.AcademicYearId == input.SourceAcademicYearId && selectedIds.Contains(x.Id));
		var sourceHeads = await feeHeadRepository.GetListAsync(x => x.AcademicYearId == input.SourceAcademicYearId && x.IsCurrentRevision);
		var result = new FeeCategoryCopyResult();
		foreach (var item in preview.Items)
		{
			resolutions.TryGetValue(item.SourceCategoryId, out var resolution);
			if (item.Classification == "Identical" || string.Equals(resolution?.Action, "Skip", StringComparison.OrdinalIgnoreCase) || string.Equals(resolution?.Action, "Keep", StringComparison.OrdinalIgnoreCase))
			{
				result.Skipped++;
				continue;
			}
			if (item.Classification == "Conflict" && string.IsNullOrWhiteSpace(resolution?.NewName))
				throw new BusinessException(FinanceFeeCategoryErrorCodes.CopyConflict).WithData("Category", item.Name);
			var source = sourceCategories.Single(x => x.Id == item.SourceCategoryId);
			var name = string.IsNullOrWhiteSpace(resolution?.NewName) ? source.Name : resolution.NewName;
			await feeCategoryManager.EnsureNameIsUniqueAsync(input.DestinationAcademicYearId, name);
			var category = new FeeCategory(GuidGenerator.Create(), CurrentTenant.Id, input.DestinationAcademicYearId, name, source.Description);
			category.SetStatus(source.IsActive,
				source.IsActive ? null : source.DeactivationReason ?? "Copied as inactive from the source academic year.");
			await feeCategoryRepository.InsertAsync(category);
			var heads = new List<FeeHead>();
			foreach (var sourceHead in sourceHeads.Where(x => x.FeeCategoryId == source.Id))
			{
				var head = new FeeHead(GuidGenerator.Create(), CurrentTenant.Id, input.DestinationAcademicYearId, category.Id,
					sourceHead.Name, sourceHead.Module, sourceHead.IsConcessionApplied, sourceHead.Description, sourceHead.IsActive);
				if (!sourceHead.IsActive)
					head.SetActiveStatus(false,
						sourceHead.DeactivationReason ?? "Copied as inactive from the source academic year.");
				await feeHeadRepository.InsertAsync(head);
				heads.Add(head);
			}
			try
			{
				await CurrentUnitOfWork.SaveChangesAsync();
			}
			catch (Exception exception) when (FeeSetupPersistenceErrors.IsCategoryNameConflict(exception))
			{
				throw new BusinessException(FinanceFeeCategoryErrorCodes.DuplicateName).WithData("Name", name);
			}
			result.Categories.Add(await MapAsync(category, heads));
			result.Created++;
		}
		if (result.Created > 0) await referenceCache.InvalidateFeeHeadsAsync();
		return result;
	}

	private async Task<FeeCategoryResponse> MapAsync(FeeCategory category, List<FeeHead> heads) =>
		Map(category, heads, await usageService.GetUsageAsync(heads.Select(x => x.Id)));

	private FeeCategoryResponse Map(FeeCategory category, List<FeeHead> heads, Dictionary<Guid, List<string>> usage)
	{
		var response = ObjectMapper.Map<FeeCategory, FeeCategoryResponse>(category);
		response.ConcurrencyStamp = category.ConcurrencyStamp;
		response.FeeHeads = heads.OrderByDescending(x => x.IsCurrentRevision).ThenBy(x => x.Name).Select(head =>
		{
			var dto = ObjectMapper.Map<FeeHead, FeeHeadResponse>(head);
			dto.FeeCategoryName = category.Name;
			dto.ConcurrencyStamp = head.ConcurrencyStamp;
			dto.UsageBlockers = usage.GetValueOrDefault(head.Id) ?? [];
			dto.IsUsedInDownstream = dto.UsageBlockers.Count > 0;
			dto.IsEffectivelyActive = category.IsActive && head.IsActive && head.IsCurrentRevision;
			return dto;
		}).ToList();
		response.UsageBlockers = response.FeeHeads.SelectMany(x => x.UsageBlockers).Distinct().ToList();
		response.IsUsedInDownstream = response.UsageBlockers.Count > 0;
		return response;
	}

	private static bool HeadsMatch(IEnumerable<FeeHead> source, IEnumerable<FeeHead> destination) =>
		source.Select(x => (x.NormalizedName, x.Module, x.IsConcessionApplied, x.IsActive, x.Description)).OrderBy(x => x.NormalizedName)
			.SequenceEqual(destination.Select(x => (x.NormalizedName, x.Module, x.IsConcessionApplied, x.IsActive, x.Description)).OrderBy(x => x.NormalizedName));

	private static bool HeadMatches(FeeHead source, FeeHead destination) =>
		source.NormalizedName == destination.NormalizedName &&
		source.Module == destination.Module &&
		source.IsConcessionApplied == destination.IsConcessionApplied &&
		source.IsActive == destination.IsActive &&
		source.Description == destination.Description;

	private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

	private static string SearchText(string name, string? description) =>
		FeeSetupNameNormalizer.ForComparison($"{name} {description}");

	private static void ValidatePage(int page, int pageSize)
	{
		if (page < 1 || pageSize is < 1 or > 100)
			throw new BusinessException(FinanceFeeCategoryErrorCodes.InvalidQuery).WithData("Page", page).WithData("PageSize", pageSize);
	}

	private static void EnsureConcurrency(string actual, string supplied, string code)
	{
		if (!string.Equals(actual, supplied, StringComparison.Ordinal)) throw new BusinessException(code);
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.ObjectMapping;

namespace ShikshaMitra.HR.Payroll.Settings;

public class DeductionComponentReader(
	IRepository<DeductionComponent, Guid> deductionRepository,
	IRepository<EarningComponent, Guid> earningRepository,
	IDistributedCache<PagedResultDto<DeductionComponentDto>> cache,
	ICurrentTenant currentTenant,
	IObjectMapper objectMapper,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	private const string CachePrefix = "DeductionComponents";

	public async Task<DeductionComponentDto> GetAsync(Guid id)
	{
		var deductionComponent = await deductionRepository.GetAsync(id);
		var dto = objectMapper.Map<DeductionComponent, DeductionComponentDto>(deductionComponent);
		return await PopulateSelectedComponentDataAsync(dto);
	}

	public async Task<PagedResultDto<DeductionComponentDto>> GetFilteredListAsync(
		int skipCount,
		int maxResultCount,
		string? search)
	{
		var queryable = (await deductionRepository.GetQueryableAsync()).Where(x => x.IsActive);

		if (!string.IsNullOrWhiteSpace(search))
		{
			var term = search.Trim().ToLower();
			queryable = queryable.Where(x => x.Name.ToLower().Contains(term));
		}

		queryable = queryable.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name);

		var totalCount = await asyncExecuter.CountAsync(queryable);
		var items = await asyncExecuter.ToListAsync(
			queryable
				.Skip(skipCount)
				.Take(maxResultCount));

		var mappedItems = objectMapper.Map<List<DeductionComponent>, List<DeductionComponentDto>>(items);
		await PopulateSelectedComponentDataAsync(mappedItems);

		return new PagedResultDto<DeductionComponentDto>(totalCount, mappedItems);
	}

	public async Task<PagedResultDto<DeductionComponentDto>> GetAllActiveAsync(PagedResultRequestDto input)
	{
		var activeCacheKey = PayrollComponentImpactProjection.BuildActiveCacheKey(
			CachePrefix,
			currentTenant.Id,
			input.SkipCount,
			input.MaxResultCount);

		var cached = await cache.GetAsync(activeCacheKey);
		if (cached != null)
		{
			return cached;
		}

		var queryable = await deductionRepository.GetQueryableAsync();
		var query = queryable.Where(x => x.IsActive);

		var totalCount = await asyncExecuter.CountAsync(query);
		var items = await asyncExecuter.ToListAsync(
			query.OrderBy(x => x.DisplayOrder)
				.ThenBy(x => x.Name)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount));

		var result = new PagedResultDto<DeductionComponentDto>(
			totalCount,
			objectMapper.Map<List<DeductionComponent>, List<DeductionComponentDto>>(items));

		await PopulateSelectedComponentDataAsync(result.Items);

		await cache.SetAsync(
			activeCacheKey,
			result,
			new DistributedCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
			});

		return result;
	}

	private async Task<DeductionComponentDto> PopulateSelectedComponentDataAsync(DeductionComponentDto dto)
	{
		if (!dto.SelectedDeductionComponentId.HasValue)
		{
			return dto;
		}

		var selectedDeduction = await deductionRepository.FindAsync(dto.SelectedDeductionComponentId.Value);
		if (selectedDeduction is { IsActive: true })
		{
			dto.SelectedDeductionComponentName = selectedDeduction.Name;
			dto.SelectedDeductionComponentDescription = selectedDeduction.Description;
			dto.SelectedDeductionComponentVisibleInPayslip = selectedDeduction.VisibleInPayslip;
			dto.SelectedDeductionComponentSource = "DEDUCTION_COMPONENT";

			return dto;
		}

		var selectedEarning = await earningRepository.FindAsync(dto.SelectedDeductionComponentId.Value);
		if (selectedEarning is { IsActive: true })
		{
			dto.SelectedDeductionComponentName = selectedEarning.Name;
			dto.SelectedDeductionComponentDescription = selectedEarning.Description;
			dto.SelectedDeductionComponentVisibleInPayslip = selectedEarning.VisibleInPayslip;
			dto.SelectedDeductionComponentSource = "EARNING_COMPONENT";
		}

		return dto;
	}

	private async Task PopulateSelectedComponentDataAsync(IReadOnlyList<DeductionComponentDto> items)
	{
		var selectedIds = items
			.Where(x => x.SelectedDeductionComponentId.HasValue)
			.Select(x => x.SelectedDeductionComponentId!.Value)
			.Distinct()
			.ToList();

		if (selectedIds.Count == 0)
		{
			return;
		}

		var deductionQueryable = await deductionRepository.GetQueryableAsync();
		var selectedDeductions = await asyncExecuter.ToListAsync(
			deductionQueryable.Where(x => x.IsActive && selectedIds.Contains(x.Id)));

		var selectedDeductionById = selectedDeductions.ToDictionary(x => x.Id);
		var unresolvedIds = selectedIds
			.Where(id => !selectedDeductionById.ContainsKey(id))
			.ToList();

		var selectedEarningById = new Dictionary<Guid, EarningComponent>();
		if (unresolvedIds.Count > 0)
		{
			var earningQueryable = await earningRepository.GetQueryableAsync();
			var selectedEarnings = await asyncExecuter.ToListAsync(
				earningQueryable.Where(x => x.IsActive && unresolvedIds.Contains(x.Id)));
			selectedEarningById = selectedEarnings.ToDictionary(x => x.Id);
		}

		foreach (var item in items)
		{
			if (!item.SelectedDeductionComponentId.HasValue)
			{
				continue;
			}

			if (selectedDeductionById.TryGetValue(item.SelectedDeductionComponentId.Value, out var selectedDeduction))
			{
				item.SelectedDeductionComponentName = selectedDeduction.Name;
				item.SelectedDeductionComponentDescription = selectedDeduction.Description;
				item.SelectedDeductionComponentVisibleInPayslip = selectedDeduction.VisibleInPayslip;
				item.SelectedDeductionComponentSource = "DEDUCTION_COMPONENT";
				continue;
			}

			if (selectedEarningById.TryGetValue(item.SelectedDeductionComponentId.Value, out var selectedEarning))
			{
				item.SelectedDeductionComponentName = selectedEarning.Name;
				item.SelectedDeductionComponentDescription = selectedEarning.Description;
				item.SelectedDeductionComponentVisibleInPayslip = selectedEarning.VisibleInPayslip;
				item.SelectedDeductionComponentSource = "EARNING_COMPONENT";
			}
		}
	}
}

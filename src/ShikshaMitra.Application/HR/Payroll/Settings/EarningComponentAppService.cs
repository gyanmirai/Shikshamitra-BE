using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.Settings;

[RemoteService(false)]
public class EarningComponentAppService : CrudAppService<
	EarningComponent,
	EarningComponentDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateEarningComponentDto,
	UpdateEarningComponentDto>, IEarningComponentAppService
{
	private readonly EarningComponentManager _earningComponentManager;
	private readonly IEmployeePayrollComponentRepository _employeePayrollComponentRepository;
	private readonly IEmployeeCacheManager _employeeCacheManager;
	private readonly IRepository<Employee, Guid> _employeeRepository;
	private readonly ISalarySlipRepository _salarySlipRepository;
	private readonly IRepository<PayrollComponentVersion, Guid> _componentVersionRepository;
	private readonly IDistributedCache<PagedResultDto<EarningComponentDto>> _cache;

	private string GetBaseCacheKey() =>
		$"EarningComponents:Active:Tenant:{CurrentTenant?.Id ?? Guid.Empty}";

	private string GetActiveCacheKey(int skipCount, int maxResultCount) =>
		$"{GetBaseCacheKey()}:Skip:{skipCount}:Take:{maxResultCount}";

	public EarningComponentAppService(
		IRepository<EarningComponent, Guid> repository,
		EarningComponentManager earningComponentManager,
		IEmployeePayrollComponentRepository employeePayrollComponentRepository,
		IEmployeeCacheManager employeeCacheManager,
		IRepository<Employee, Guid> employeeRepository,
		ISalarySlipRepository salarySlipRepository,
		IRepository<PayrollComponentVersion, Guid> componentVersionRepository,
		IDistributedCache<PagedResultDto<EarningComponentDto>> cache)
		: base(repository)
	{
		_earningComponentManager = earningComponentManager;
		_employeePayrollComponentRepository = employeePayrollComponentRepository;
		_employeeCacheManager = employeeCacheManager;
		_employeeRepository = employeeRepository;
		_salarySlipRepository = salarySlipRepository;
		_componentVersionRepository = componentVersionRepository;
		_cache = cache;
	}

	public override async Task<EarningComponentDto> CreateAsync(CreateEarningComponentDto input)
	{
		ArgumentNullException.ThrowIfNull(input);

		var earningComponent = await _earningComponentManager.CreateAsync(
			input.Name,
			input.Description,
			input.ComponentType,
			input.CalculationType,
			input.Amount,
			input.Percentage,
			input.SelectedEarningComponentId,
			input.DependOn,
			input.VisibleInPayslip,
			input.DisplayOrder
		);

		try
		{
			await Repository.InsertAsync(earningComponent, autoSave: true);
		}
		catch (Exception ex) when (IsEarningComponentNameUniqueViolation(ex))
		{
			throw new BusinessException("ShikshaMitra:EarningComponentNameAlreadyExists")
				.WithData("name", input.Name);
		}

		await ClearCacheAsync();
		await SyncLinkedEmployeeEarningComponentsAsync(earningComponent);

		return ObjectMapper.Map<EarningComponent, EarningComponentDto>(earningComponent);
	}

	public override async Task<EarningComponentDto> UpdateAsync(Guid id, UpdateEarningComponentDto input)
	{
		ArgumentNullException.ThrowIfNull(input);

		var earningComponent = await Repository.GetAsync(id);
		EnsureActive(earningComponent);
		if (string.IsNullOrWhiteSpace(input.PreviewHash))
		{
			throw new BusinessException("ShikshaMitra:PayrollComponentPreviewRequired")
				.WithData("message", "Preview payroll component impact before saving.");
		}

		var currentPreview = await PreviewUpdateImpactAsync(id, input);
		if (!string.Equals(currentPreview.PreviewHash, input.PreviewHash, StringComparison.Ordinal))
		{
			throw new BusinessException("ShikshaMitra:PayrollComponentPreviewExpired")
				.WithData("message", "Payroll component impact preview is no longer current. Please preview again before saving.");
		}

		await _earningComponentManager.UpdateAsync(
			earningComponent,
			input.Name,
			input.Description,
			input.ComponentType,
			input.CalculationType,
			input.Amount,
			input.Percentage,
			input.SelectedEarningComponentId,
			input.DependOn,
			input.VisibleInPayslip,
			input.DisplayOrder
		);

		try
		{
			await Repository.UpdateAsync(earningComponent, autoSave: true);
		}
		catch (Exception ex) when (IsEarningComponentNameUniqueViolation(ex))
		{
			throw new BusinessException("ShikshaMitra:EarningComponentNameAlreadyExists")
				.WithData("name", input.Name);
		}

		var effectiveMonth = ResolveEffectiveMonth(input.EffectiveMonth);
		await ClearCacheAsync();
		await _componentVersionRepository.InsertAsync(
			PayrollComponentVersion.FromEarningComponent(GuidGenerator.Create(), effectiveMonth, earningComponent),
			autoSave: true);
		await SyncLinkedEmployeeEarningComponentsAsync(earningComponent, input.ApplyMode, effectiveMonth);

		return ObjectMapper.Map<EarningComponent, EarningComponentDto>(earningComponent);
	}

	public async Task<PayrollComponentUpdateImpactPreviewDto> PreviewUpdateImpactAsync(
		Guid id,
		UpdateEarningComponentDto input,
		bool includeEmployees = false,
		int skipCount = 0,
		int maxResultCount = 20)
	{
		ArgumentNullException.ThrowIfNull(input);

		var earningComponent = await Repository.GetAsync(id);
		EnsureActive(earningComponent);
		var linkedRows = await _employeePayrollComponentRepository.GetByComponentIdAsync(id, isEarning: true);
		var employeeIds = linkedRows.Select(x => x.EmployeeId).Distinct().ToList();
		var effectiveMonth = ResolveEffectiveMonth(input.EffectiveMonth);
		var slips = await _salarySlipRepository.GetByMonthAsync(effectiveMonth);
		var linkedSlipRows = slips.Where(x => employeeIds.Contains(x.EmployeeId)).ToList();
		var finalizedSlipCount = linkedSlipRows.Count(x => x.Status == SalarySlipStatus.Finalized);
		var draftSlipCount = linkedSlipRows.Count(x => x.Status == SalarySlipStatus.Draft);
		var employeeNames = await GetEmployeeNamesAsync(employeeIds);

		var syncedRows = linkedRows.Where(x => x.UsesMasterValue).ToList();
		var overrideRows = linkedRows.Where(x => !x.UsesMasterValue).ToList();

		return new PayrollComponentUpdateImpactPreviewDto
		{
			LinkedEmployeeCount = employeeIds.Count,
			SyncedEmployeeCount = syncedRows.Select(x => x.EmployeeId).Distinct().Count(),
			ManualOverrideEmployeeCount = overrideRows.Select(x => x.EmployeeId).Distinct().Count(),
			UnaffectedEmployeeCount = await GetUnaffectedEmployeeCountAsync(employeeIds),
			FinalizedSlipCount = finalizedSlipCount,
			DraftSlipCount = draftSlipCount,
			EffectiveMonth = effectiveMonth,
			PreviewHash = BuildPreviewHash(
				id,
				input,
				effectiveMonth,
				syncedRows.Count,
				overrideRows.Count,
				finalizedSlipCount,
				draftSlipCount),
			SyncedEmployees = BuildEmployeeSamples(syncedRows, employeeNames, includeEmployees, skipCount, maxResultCount),
			ManualOverrideEmployees = BuildEmployeeSamples(overrideRows, employeeNames, includeEmployees, skipCount, maxResultCount)
		};
	}

	public async Task<PayrollComponentUpdateImpactPreviewDto> PreviewDeleteImpactAsync(
		Guid id,
		bool includeEmployees = false,
		int skipCount = 0,
		int maxResultCount = 20)
	{
		await Repository.GetAsync(id);
		var linkedRows = await _employeePayrollComponentRepository.GetByComponentIdAsync(id, isEarning: true);
		var employeeIds = linkedRows.Select(x => x.EmployeeId).Distinct().ToList();
		var month = Clock.Now.ToString("yyyy-MM");
		var slipCounts = await _salarySlipRepository.GetStatusCountsByEmployeeIdsAsync(employeeIds);
		var employeeNames = await GetEmployeeNamesAsync(employeeIds);
		var syncedRows = linkedRows.Where(x => x.UsesMasterValue).ToList();
		var overrideRows = linkedRows.Where(x => !x.UsesMasterValue).ToList();

		return new PayrollComponentUpdateImpactPreviewDto
		{
			LinkedEmployeeCount = employeeIds.Count,
			SyncedEmployeeCount = syncedRows.Select(x => x.EmployeeId).Distinct().Count(),
			ManualOverrideEmployeeCount = overrideRows.Select(x => x.EmployeeId).Distinct().Count(),
			UnaffectedEmployeeCount = await GetUnaffectedEmployeeCountAsync(employeeIds),
			FinalizedSlipCount = slipCounts.FinalizedSlipCount,
			DraftSlipCount = slipCounts.DraftSlipCount,
			EffectiveMonth = month,
			PreviewHash = BuildDeletePreviewHash(
				id,
				employeeIds,
				syncedRows.Count,
				overrideRows.Count,
				slipCounts.FinalizedSlipCount,
				slipCounts.DraftSlipCount),
			SyncedEmployees = BuildEmployeeSamples(syncedRows, employeeNames, includeEmployees, skipCount, maxResultCount),
			ManualOverrideEmployees = BuildEmployeeSamples(overrideRows, employeeNames, includeEmployees, skipCount, maxResultCount)
		};
	}

	public override Task DeleteAsync(Guid id)
	{
		throw new BusinessException("ShikshaMitra:PayrollComponentPreviewRequired")
			.WithData("message", "Preview payroll component delete impact before deleting.");
	}

	public async Task DeleteConfirmedAsync(Guid id, string previewHash)
	{
		if (string.IsNullOrWhiteSpace(previewHash))
		{
			throw new BusinessException("ShikshaMitra:PayrollComponentPreviewRequired")
				.WithData("message", "Preview payroll component delete impact before deleting.");
		}

		var currentPreview = await PreviewDeleteImpactAsync(id);
		if (!string.Equals(currentPreview.PreviewHash, previewHash, StringComparison.Ordinal))
		{
			throw new BusinessException("ShikshaMitra:PayrollComponentPreviewExpired")
				.WithData("message", "Payroll component delete impact preview is no longer current. Please preview again before deleting.");
		}

		await DeleteConfirmedInternalAsync(id);
	}

	private async Task DeleteConfirmedInternalAsync(Guid id)
	{
		var earningComponent = await Repository.GetAsync(id);
		var linkedRows = await _employeePayrollComponentRepository.GetByComponentIdAsync(id, isEarning: true);
		var linkedEmployeeIds = linkedRows.Select(x => x.EmployeeId).Distinct().ToList();
		earningComponent.Deactivate();
		await Repository.UpdateAsync(earningComponent, autoSave: false);
		await _employeePayrollComponentRepository.DeleteByComponentIdAsync(id, isEarning: true);

		if (UnitOfWorkManager.Current != null)
		{
			await UnitOfWorkManager.Current.SaveChangesAsync();
		}

		await ClearCacheAsync();
		foreach (var employeeId in linkedEmployeeIds)
		{
			await _employeeCacheManager.InvalidateEmployeeAsync(employeeId);
		}
	}

	public override async Task<PagedResultDto<EarningComponentDto>> GetListAsync(PagedAndSortedResultRequestDto input)
	{
		return await GetFilteredListAsync(input.SkipCount, input.MaxResultCount, null);
	}

	public override async Task<EarningComponentDto> GetAsync(Guid id)
	{
		var dto = await base.GetAsync(id);
		return await PopulateSelectedComponentDataAsync(dto);
	}

	public async Task<PagedResultDto<EarningComponentDto>> GetFilteredListAsync(int skipCount, int maxResultCount, string? search)
	{
		var queryable = (await Repository.GetQueryableAsync()).Where(x => x.IsActive);

		if (!string.IsNullOrWhiteSpace(search))
		{
			var term = search.Trim().ToLower();
			queryable = queryable.Where(x => x.Name.ToLower().Contains(term));
		}

		queryable = queryable.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name);

		var totalCount = await AsyncExecuter.CountAsync(queryable);

		var items = await AsyncExecuter.ToListAsync(
			queryable
				.Skip(skipCount)
				.Take(maxResultCount)
		);

		var mappedItems = ObjectMapper.Map<List<EarningComponent>, List<EarningComponentDto>>(items);
		await PopulateSelectedComponentDataAsync(mappedItems);

		return new PagedResultDto<EarningComponentDto>(totalCount, mappedItems);
	}

	public async Task<PagedResultDto<EarningComponentDto>> GetAllActiveAsync(PagedResultRequestDto input)
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
			query.OrderBy(x => x.DisplayOrder)
				.ThenBy(x => x.Name)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount)
		);

		var result = new PagedResultDto<EarningComponentDto>(
			totalCount,
			ObjectMapper.Map<List<EarningComponent>, List<EarningComponentDto>>(items)
		);

		await PopulateSelectedComponentDataAsync(result.Items);

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

	public async Task ActivateAsync(Guid id)
	{
		var earningComponent = await Repository.GetAsync(id);
		earningComponent.Activate();
		await Repository.UpdateAsync(earningComponent, autoSave: true);
		await ClearCacheAsync();
	}

	public async Task DeactivateAsync(Guid id)
	{
		var earningComponent = await Repository.GetAsync(id);
		earningComponent.Deactivate();
		await Repository.UpdateAsync(earningComponent, autoSave: true);
		await ClearCacheAsync();
	}

	private async Task ClearCacheAsync()
	{
		var cacheKeysToRemove = new List<string>();
		int[] commonPageSizes = { 10, 20, 25, 50, 100 };
		int maxPages = 10;

		foreach (var pageSize in commonPageSizes)
		{
			for (int page = 0; page < maxPages; page++)
			{
				int skipCount = page * pageSize;
				cacheKeysToRemove.Add(GetActiveCacheKey(skipCount, pageSize));
			}
		}

		try
		{
			await _cache.RemoveManyAsync(cacheKeysToRemove);
		}
		catch (Exception ex)
		{
			Logger.LogWarning(
				ex,
				"Failed to invalidate earning component cache for tenant {TenantId}. List endpoints may be stale until expiration.",
				CurrentTenant?.Id);
		}
	}

	/// <summary>
	/// Maps DB unique violations on (TenantId, Name) to a handled business error (e.g. race after <see cref="EarningComponentManager"/> check).
	/// </summary>
	private static bool IsEarningComponentNameUniqueViolation(Exception exception)
	{
		const string indexName = "IX_EarningComponents_TenantId_Name";
		const string tableHint = "AppEarningComponents";

		for (var ex = exception; ex != null; ex = ex.InnerException!)
		{
			var message = ex.Message;
			if (string.IsNullOrEmpty(message))
			{
				continue;
			}

			if (message.Contains(indexName, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (message.Contains(tableHint, StringComparison.OrdinalIgnoreCase)
			    && message.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			// PostgreSQL: 23505 = unique_violation
			if (message.Contains("23505", StringComparison.Ordinal)
			    && (message.Contains(tableHint, StringComparison.OrdinalIgnoreCase)
			        || message.Contains(indexName, StringComparison.OrdinalIgnoreCase)))
			{
				return true;
			}
		}

		return false;
	}

	private async Task SyncLinkedEmployeeEarningComponentsAsync(
		EarningComponent earningComponent,
		PayrollComponentApplyMode applyMode = PayrollComponentApplyMode.SyncedOnly,
		string? effectiveMonth = null)
	{
		var linkedRows = await _employeePayrollComponentRepository.GetByComponentIdAsync(
			earningComponent.Id,
			isEarning: true);

		if (IsFutureEffectiveMonth(effectiveMonth))
		{
			await MarkFutureSyncedRowsAsync(linkedRows, applyMode);
			return;
		}

		var rowsToSync = linkedRows
			.Where(x => applyMode == PayrollComponentApplyMode.ForceApply || x.UsesMasterValue)
			.ToList();

		if (rowsToSync.Count == 0)
		{
			return;
		}

		foreach (var row in rowsToSync)
		{
			row.UpdateComponent(
				earningComponent.Name,
				earningComponent.Description,
				ResolveComponentAmount(earningComponent, row.Amount),
				earningComponent.CalculationType == CalculationType.Amount,
				row.IsTemp,
				earningComponent.CalculationType == CalculationType.Amount
					? SalaryComponentType.Fixed
					: SalaryComponentType.Percentage,
				MapDependency(earningComponent.DependOn),
				usesMasterValue: true);

			await _employeePayrollComponentRepository.UpdateAsync(row, autoSave: false);
		}

		if (UnitOfWorkManager.Current != null)
		{
			await UnitOfWorkManager.Current.SaveChangesAsync();
		}

		foreach (var employeeId in rowsToSync.Select(x => x.EmployeeId).Distinct())
		{
			await _employeeCacheManager.InvalidateEmployeeAsync(employeeId);
		}
	}

	private async Task MarkFutureSyncedRowsAsync(
		IReadOnlyList<EmployeePayrollComponent> linkedRows,
		PayrollComponentApplyMode applyMode)
	{
		if (applyMode != PayrollComponentApplyMode.ForceApply)
		{
			return;
		}

		var rowsToReset = linkedRows.Where(x => !x.UsesMasterValue).ToList();
		if (rowsToReset.Count == 0)
		{
			return;
		}

		foreach (var row in rowsToReset)
		{
			row.MarkUsesMasterValue();
			await _employeePayrollComponentRepository.UpdateAsync(row, autoSave: false);
		}

		if (UnitOfWorkManager.Current != null)
		{
			await UnitOfWorkManager.Current.SaveChangesAsync();
		}

		foreach (var employeeId in rowsToReset.Select(x => x.EmployeeId).Distinct())
		{
			await _employeeCacheManager.InvalidateEmployeeAsync(employeeId);
		}
	}

	private async Task<Dictionary<Guid, string>> GetEmployeeNamesAsync(IReadOnlyCollection<Guid> employeeIds)
	{
		if (employeeIds.Count == 0)
		{
			return new Dictionary<Guid, string>();
		}

		var queryable = await _employeeRepository.GetQueryableAsync();
		var employees = await AsyncExecuter.ToListAsync(queryable.Where(x => employeeIds.Contains(x.Id)));
		return employees.ToDictionary(
			x => x.Id,
			x => string.IsNullOrWhiteSpace(x.EmployeeName) ? x.EmployeeCode : x.EmployeeName);
	}

	private async Task<int> GetUnaffectedEmployeeCountAsync(IReadOnlyCollection<Guid> linkedEmployeeIds)
	{
		var queryable = await _employeeRepository.GetQueryableAsync();
		var totalEmployees = await AsyncExecuter.CountAsync(queryable);
		return Math.Max(0, totalEmployees - linkedEmployeeIds.Count);
	}

	private static IReadOnlyList<PayrollComponentImpactEmployeeDto> BuildEmployeeSamples(
		IReadOnlyList<EmployeePayrollComponent> rows,
		IReadOnlyDictionary<Guid, string> employeeNames,
		bool includeEmployees,
		int skipCount,
		int maxResultCount)
	{
		var take = includeEmployees ? maxResultCount : Math.Min(maxResultCount, 5);
		return rows
			.GroupBy(x => x.EmployeeId)
			.Skip(skipCount)
			.Take(take)
			.Select(group =>
			{
				var row = group.First();
				return new PayrollComponentImpactEmployeeDto
				{
					EmployeeId = row.EmployeeId,
					EmployeeName = employeeNames.TryGetValue(row.EmployeeId, out var name) ? name : row.EmployeeId.ToString(),
					CurrentAmount = row.Amount,
					UsesMasterValue = row.UsesMasterValue
				};
			})
			.ToList();
	}

	private string ResolveEffectiveMonth(string? effectiveMonth)
	{
		if (DateTime.TryParse($"{effectiveMonth}-01", out var parsed))
		{
			return parsed.ToString("yyyy-MM");
		}

		return Clock.Now.AddMonths(1).ToString("yyyy-MM");
	}

	private bool IsFutureEffectiveMonth(string? effectiveMonth)
	{
		if (string.IsNullOrWhiteSpace(effectiveMonth))
		{
			return false;
		}

		return string.Compare(effectiveMonth, Clock.Now.ToString("yyyy-MM"), StringComparison.Ordinal) > 0;
	}

	private static string BuildPreviewHash(
		Guid componentId,
		UpdateEarningComponentDto input,
		string effectiveMonth,
		int syncedCount,
		int overrideCount,
		int finalizedSlipCount,
		int draftSlipCount)
	{
		var raw = string.Join("|",
			componentId,
			input.Name,
			input.Description,
			input.ComponentType,
			input.CalculationType,
			input.Amount,
			input.Percentage,
			input.SelectedEarningComponentId,
			input.DependOn,
			input.VisibleInPayslip,
			effectiveMonth,
			syncedCount,
			overrideCount,
			finalizedSlipCount,
			draftSlipCount);
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
	}

	private static string BuildDeletePreviewHash(
		Guid componentId,
		IReadOnlyCollection<Guid> employeeIds,
		int syncedCount,
		int overrideCount,
		int finalizedSlipCount,
		int draftSlipCount)
	{
		var orderedEmployeeIds = string.Join(",", employeeIds.OrderBy(x => x));
		var raw = string.Join("|",
			"delete",
			componentId,
			orderedEmployeeIds,
			employeeIds.Count,
			syncedCount,
			overrideCount,
			finalizedSlipCount,
			draftSlipCount);
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
	}

	private static SalaryComponentDependency? MapDependency(ComponentDependency dependOn)
	{
		return dependOn switch
		{
			ComponentDependency.None => SalaryComponentDependency.None,
			ComponentDependency.Basic => SalaryComponentDependency.Basic,
			ComponentDependency.Gross => SalaryComponentDependency.Gross,
			_ => null
		};
	}

	private static void EnsureActive(EarningComponent earningComponent)
	{
		if (earningComponent.IsActive)
		{
			return;
		}

		throw new BusinessException("ShikshaMitra:PayrollComponentInactive")
			.WithData("message", "Retired payroll components cannot be updated.");
	}

	private static decimal ResolveComponentAmount(EarningComponent earningComponent, decimal fallbackAmount)
	{
		return earningComponent.CalculationType == CalculationType.Amount
			? earningComponent.Amount ?? fallbackAmount
			: earningComponent.Percentage ?? fallbackAmount;
	}

	private IQueryable<EarningComponent> ApplySorting(IQueryable<EarningComponent> queryable, string sorting)
	{
		if (string.IsNullOrWhiteSpace(sorting))
		{
			return queryable.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name);
		}

		var sortingParts = sorting.Split(',');
		IOrderedQueryable<EarningComponent>? orderedQueryable = null;
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
					"name" => direction == "DESC"
						? queryable.OrderByDescending(x => x.Name)
						: queryable.OrderBy(x => x.Name),
					"componenttype" => direction == "DESC"
						? queryable.OrderByDescending(x => x.ComponentType)
						: queryable.OrderBy(x => x.ComponentType),
					"displayorder" => direction == "DESC"
						? queryable.OrderByDescending(x => x.DisplayOrder)
						: queryable.OrderBy(x => x.DisplayOrder),
					"isactive" => direction == "DESC"
						? queryable.OrderByDescending(x => x.IsActive)
						: queryable.OrderBy(x => x.IsActive),
					_ => queryable.OrderBy(x => x.DisplayOrder)
				};
				isFirstSort = false;
			}
			else
			{
				orderedQueryable = property.ToLower() switch
				{
					"name" => direction == "DESC"
						? orderedQueryable!.ThenByDescending(x => x.Name)
						: orderedQueryable!.ThenBy(x => x.Name),
					"componenttype" => direction == "DESC"
						? orderedQueryable!.ThenByDescending(x => x.ComponentType)
						: orderedQueryable!.ThenBy(x => x.ComponentType),
					"displayorder" => direction == "DESC"
						? orderedQueryable!.ThenByDescending(x => x.DisplayOrder)
						: orderedQueryable!.ThenBy(x => x.DisplayOrder),
					"isactive" => direction == "DESC"
						? orderedQueryable!.ThenByDescending(x => x.IsActive)
						: orderedQueryable!.ThenBy(x => x.IsActive),
					_ => orderedQueryable!.ThenBy(x => x.Name)
				};
			}
		}

		return orderedQueryable ?? queryable.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name);
	}

	private async Task<EarningComponentDto> PopulateSelectedComponentDataAsync(EarningComponentDto dto)
	{
		if (!dto.SelectedEarningComponentId.HasValue)
		{
			return dto;
		}

		var selected = await Repository.FindAsync(dto.SelectedEarningComponentId.Value);
		if (selected is null)
		{
			return dto;
		}

		dto.SelectedEarningComponentName = selected.Name;
		dto.SelectedEarningComponentDescription = selected.Description;
		dto.SelectedEarningComponentVisibleInPayslip = selected.VisibleInPayslip;

		return dto;
	}

	private async Task PopulateSelectedComponentDataAsync(IReadOnlyList<EarningComponentDto> items)
	{
		var selectedIds = items
			.Where(x => x.SelectedEarningComponentId.HasValue)
			.Select(x => x.SelectedEarningComponentId!.Value)
			.Distinct()
			.ToList();

		if (selectedIds.Count == 0)
		{
			return;
		}

		var queryable = await Repository.GetQueryableAsync();
		var selectedComponents = await AsyncExecuter.ToListAsync(
			queryable.Where(x => selectedIds.Contains(x.Id)));

		var selectedById = selectedComponents.ToDictionary(x => x.Id);

		foreach (var item in items)
		{
			if (!item.SelectedEarningComponentId.HasValue)
			{
				continue;
			}

			if (!selectedById.TryGetValue(item.SelectedEarningComponentId.Value, out var selected))
			{
				continue;
			}

			item.SelectedEarningComponentName = selected.Name;
			item.SelectedEarningComponentDescription = selected.Description;
			item.SelectedEarningComponentVisibleInPayslip = selected.VisibleInPayslip;
		}
	}
}

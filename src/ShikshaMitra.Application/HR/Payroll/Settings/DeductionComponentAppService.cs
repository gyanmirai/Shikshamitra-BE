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
public class DeductionComponentAppService : CrudAppService<
	DeductionComponent,
	DeductionComponentDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateDeductionComponentDto,
	UpdateDeductionComponentDto>, IDeductionComponentAppService
{
	private readonly DeductionComponentManager _deductionComponentManager;
	private readonly IEmployeePayrollComponentRepository _employeePayrollComponentRepository;
	private readonly IEmployeeCacheManager _employeeCacheManager;
	private readonly IRepository<Employee, Guid> _employeeRepository;
	private readonly IRepository<EarningComponent, Guid> _earningComponentRepository;
	private readonly ISalarySlipRepository _salarySlipRepository;
	private readonly IRepository<PayrollComponentVersion, Guid> _componentVersionRepository;
	private readonly IDistributedCache<PagedResultDto<DeductionComponentDto>> _cache;

	private string GetBaseCacheKey() =>
		$"DeductionComponents:Active:Tenant:{CurrentTenant?.Id ?? Guid.Empty}";

	private string GetActiveCacheKey(int skipCount, int maxResultCount) =>
		$"{GetBaseCacheKey()}:Skip:{skipCount}:Take:{maxResultCount}";

	public DeductionComponentAppService(
		IRepository<DeductionComponent, Guid> repository,
		DeductionComponentManager deductionComponentManager,
		IEmployeePayrollComponentRepository employeePayrollComponentRepository,
		IEmployeeCacheManager employeeCacheManager,
		IRepository<Employee, Guid> employeeRepository,
		IRepository<EarningComponent, Guid> earningComponentRepository,
		ISalarySlipRepository salarySlipRepository,
		IRepository<PayrollComponentVersion, Guid> componentVersionRepository,
		IDistributedCache<PagedResultDto<DeductionComponentDto>> cache)
		: base(repository)
	{
		_deductionComponentManager = deductionComponentManager;
		_employeePayrollComponentRepository = employeePayrollComponentRepository;
		_employeeCacheManager = employeeCacheManager;
		_employeeRepository = employeeRepository;
		_earningComponentRepository = earningComponentRepository;
		_salarySlipRepository = salarySlipRepository;
		_componentVersionRepository = componentVersionRepository;
		_cache = cache;
	}

	public override async Task<DeductionComponentDto> CreateAsync(CreateDeductionComponentDto input)
	{
		ArgumentNullException.ThrowIfNull(input);

		var deductionComponent = await _deductionComponentManager.CreateAsync(
			input.Name,
			input.Description,
			input.ComponentType,
			input.CalculationType,
			input.Amount,
			input.Percentage,
			input.SelectedDeductionComponentId,
			input.DependOn,
			input.VisibleInPayslip,
			input.DisplayOrder
		);

		try
		{
			await Repository.InsertAsync(deductionComponent, autoSave: true);
		}
		catch (Exception ex) when (IsDeductionComponentNameUniqueViolation(ex))
		{
			throw new BusinessException("ShikshaMitra:DeductionComponentNameAlreadyExists")
				.WithData("name", input.Name);
		}

		await ClearCacheAsync();

		return ObjectMapper.Map<DeductionComponent, DeductionComponentDto>(deductionComponent);
	}

	public override async Task<DeductionComponentDto> UpdateAsync(Guid id, UpdateDeductionComponentDto input)
	{
		ArgumentNullException.ThrowIfNull(input);

		var deductionComponent = await Repository.GetAsync(id);
		EnsureActive(deductionComponent);
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

		await _deductionComponentManager.UpdateAsync(
			deductionComponent,
			input.Name,
			input.Description,
			input.ComponentType,
			input.CalculationType,
			input.Amount,
			input.Percentage,
			input.SelectedDeductionComponentId,
			input.DependOn,
			input.VisibleInPayslip,
			input.DisplayOrder
		);

		try
		{
			await Repository.UpdateAsync(deductionComponent, autoSave: true);
		}
		catch (Exception ex) when (IsDeductionComponentNameUniqueViolation(ex))
		{
			throw new BusinessException("ShikshaMitra:DeductionComponentNameAlreadyExists")
				.WithData("name", input.Name);
		}

		var effectiveMonth = ResolveEffectiveMonth(input.EffectiveMonth);
		await ClearCacheAsync();
		await _componentVersionRepository.InsertAsync(
			PayrollComponentVersion.FromDeductionComponent(GuidGenerator.Create(), effectiveMonth, deductionComponent),
			autoSave: true);
		await SyncLinkedEmployeeDeductionComponentsAsync(deductionComponent, input.ApplyMode, effectiveMonth);

		return ObjectMapper.Map<DeductionComponent, DeductionComponentDto>(deductionComponent);
	}

	public async Task<PayrollComponentUpdateImpactPreviewDto> PreviewUpdateImpactAsync(
		Guid id,
		UpdateDeductionComponentDto input,
		bool includeEmployees = false,
		int skipCount = 0,
		int maxResultCount = 20)
	{
		ArgumentNullException.ThrowIfNull(input);

		var deductionComponent = await Repository.GetAsync(id);
		EnsureActive(deductionComponent);
		var linkedRows = await _employeePayrollComponentRepository.GetByComponentIdAsync(id, isEarning: false);
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
		var linkedRows = await _employeePayrollComponentRepository.GetByComponentIdAsync(id, isEarning: false);
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
		var deductionComponent = await Repository.GetAsync(id);
		var linkedRows = await _employeePayrollComponentRepository.GetByComponentIdAsync(id, isEarning: false);
		var linkedEmployeeIds = linkedRows.Select(x => x.EmployeeId).Distinct().ToList();
		deductionComponent.Deactivate();
		await Repository.UpdateAsync(deductionComponent, autoSave: false);
		await _employeePayrollComponentRepository.DeleteByComponentIdAsync(id, isEarning: false);

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

	public override async Task<PagedResultDto<DeductionComponentDto>> GetListAsync(PagedAndSortedResultRequestDto input)
	{
		return await GetFilteredListAsync(input.SkipCount, input.MaxResultCount, null);
	}

	public override async Task<DeductionComponentDto> GetAsync(Guid id)
	{
		var dto = await base.GetAsync(id);
		return await PopulateSelectedComponentDataAsync(dto);
	}

	public async Task<PagedResultDto<DeductionComponentDto>> GetFilteredListAsync(int skipCount, int maxResultCount, string? search)
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

		var mappedItems = ObjectMapper.Map<List<DeductionComponent>, List<DeductionComponentDto>>(items);
		await PopulateSelectedComponentDataAsync(mappedItems);

		return new PagedResultDto<DeductionComponentDto>(totalCount, mappedItems);
	}

	public async Task<PagedResultDto<DeductionComponentDto>> GetAllActiveAsync(PagedResultRequestDto input)
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

		var result = new PagedResultDto<DeductionComponentDto>(
			totalCount,
			ObjectMapper.Map<List<DeductionComponent>, List<DeductionComponentDto>>(items)
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
		var deductionComponent = await Repository.GetAsync(id);
		deductionComponent.Activate();
		await Repository.UpdateAsync(deductionComponent, autoSave: true);
		await ClearCacheAsync();
	}

	public async Task DeactivateAsync(Guid id)
	{
		var deductionComponent = await Repository.GetAsync(id);
		deductionComponent.Deactivate();
		await Repository.UpdateAsync(deductionComponent, autoSave: true);
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
				"Failed to invalidate deduction component cache for tenant {TenantId}. List endpoints may be stale until expiration.",
				CurrentTenant?.Id);
		}
	}

	/// <summary>
	/// Maps DB unique violations on (TenantId, Name) to a handled business error (e.g. race after <see cref="DeductionComponentManager"/> check).
	/// </summary>
	private static bool IsDeductionComponentNameUniqueViolation(Exception exception)
	{
		const string indexName = "IX_DeductionComponents_TenantId_Name";
		const string tableHint = "AppDeductionComponents";

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

			if (message.Contains("23505", StringComparison.Ordinal)
			    && (message.Contains(tableHint, StringComparison.OrdinalIgnoreCase)
			        || message.Contains(indexName, StringComparison.OrdinalIgnoreCase)))
			{
				return true;
			}
		}

		return false;
	}

	private async Task SyncLinkedEmployeeDeductionComponentsAsync(
		DeductionComponent deductionComponent,
		PayrollComponentApplyMode applyMode = PayrollComponentApplyMode.SyncedOnly,
		string? effectiveMonth = null)
	{
		var linkedRows = await _employeePayrollComponentRepository.GetByComponentIdAsync(
			deductionComponent.Id,
			isEarning: false);

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
				deductionComponent.Name,
				deductionComponent.Description,
				ResolveComponentAmount(deductionComponent, row.Amount),
				deductionComponent.CalculationType == CalculationType.Amount,
				row.IsTemp,
				deductionComponent.CalculationType == CalculationType.Amount
					? SalaryComponentType.Fixed
					: SalaryComponentType.Percentage,
				MapDependency(deductionComponent.DependOn),
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
		UpdateDeductionComponentDto input,
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
			input.SelectedDeductionComponentId,
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

	private static void EnsureActive(DeductionComponent deductionComponent)
	{
		if (deductionComponent.IsActive)
		{
			return;
		}

		throw new BusinessException("ShikshaMitra:PayrollComponentInactive")
			.WithData("message", "Retired payroll components cannot be updated.");
	}

	private static decimal ResolveComponentAmount(DeductionComponent deductionComponent, decimal fallbackAmount)
	{
		return deductionComponent.CalculationType == CalculationType.Amount
			? deductionComponent.Amount ?? fallbackAmount
			: deductionComponent.Percentage ?? fallbackAmount;
	}

	private IQueryable<DeductionComponent> ApplySorting(IQueryable<DeductionComponent> queryable, string sorting)
	{
		if (string.IsNullOrWhiteSpace(sorting))
		{
			return queryable.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name);
		}

		var sortingParts = sorting.Split(',');
		IOrderedQueryable<DeductionComponent>? orderedQueryable = null;
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

	private async Task<DeductionComponentDto> PopulateSelectedComponentDataAsync(DeductionComponentDto dto)
	{
		if (!dto.SelectedDeductionComponentId.HasValue)
		{
			return dto;
		}

		var selectedDeduction = await Repository.FindAsync(dto.SelectedDeductionComponentId.Value);
		if (selectedDeduction is { IsActive: true })
		{
			dto.SelectedDeductionComponentName = selectedDeduction.Name;
			dto.SelectedDeductionComponentDescription = selectedDeduction.Description;
			dto.SelectedDeductionComponentVisibleInPayslip = selectedDeduction.VisibleInPayslip;
			dto.SelectedDeductionComponentSource = "DEDUCTION_COMPONENT";

			return dto;
		}

		var selectedEarning = await _earningComponentRepository.FindAsync(dto.SelectedDeductionComponentId.Value);
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

		var deductionQueryable = await Repository.GetQueryableAsync();
		var selectedDeductions = await AsyncExecuter.ToListAsync(
			deductionQueryable.Where(x => x.IsActive && selectedIds.Contains(x.Id)));

		var selectedDeductionById = selectedDeductions.ToDictionary(x => x.Id);
		var unresolvedIds = selectedIds
			.Where(id => !selectedDeductionById.ContainsKey(id))
			.ToList();

		var selectedEarningById = new Dictionary<Guid, EarningComponent>();
		if (unresolvedIds.Count > 0)
		{
			var earningQueryable = await _earningComponentRepository.GetQueryableAsync();
			var selectedEarnings = await AsyncExecuter.ToListAsync(
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

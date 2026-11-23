using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Charges;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Settings.ChargeCategories;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Uow;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.Settings;

[RemoteService(false)]
public partial class ChargeCategoryAppService(
	IRepository<ChargeCategory, Guid> chargeCategoryRepository,
	IRepository<Charge, Guid> chargeRepository,
	IRepository<ChargeRequest, Guid> chargeRequestRepository,
	IRepository<TransactionCategory, Guid> transactionCategoryRepository,
	IRepository<AccountHead, Guid> accountHeadRepository,
	ITransactionCategoryReferenceLock referenceLock,
	FinanceScopeAccessService financeScopeAccessService
) : ApplicationService, IChargeCategoryAppService
{
	[UnitOfWork(isTransactional: true)]
    public async Task<ChargeCategoryResponse> CreateAsync(Guid academicYearId, CreateChargeCategoryRequest input)
    {
		await financeScopeAccessService.EnsureAcademicContextAccessibleAsync(academicYearId, null, null, null, null);
		var (incomeCategory, accountHead) = await ValidateAccountingMappingAsync(academicYearId, input.IncomeCategoryId);
		// Check for duplicate name within the same academic year
		var existingCategory = await chargeCategoryRepository.AnyAsync(x => x.AcademicYearId == academicYearId && x.Name == input.Name);

		if (existingCategory)
		{
			throw new BusinessException("A charge category with the same name already exists for this academic year.");
		}

		var chargeCategory = new ChargeCategory(
            GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
            input.Name,
            input.IsApprovalRequired,
            input.Description
        );
		chargeCategory.SetAccountingMapping(incomeCategory.Id, accountHead?.Id);

        await chargeCategoryRepository.InsertAsync(chargeCategory);

		return await ToResponseAsync(chargeCategory);
    }

    public async Task<PaginatedChargeCategoryResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null, bool? isActive = null, string? filters = null)
    {
		var tableQuery = ListFilter.Parse(filters);
		if (!await financeScopeAccessService.IsAcademicContextAccessibleAsync(academicYearId, null, null, null, null))
			return new PaginatedChargeCategoryResponse { CurrentPage = page, PageSize = pageSize, Items = [] };
        var query = await chargeCategoryRepository.GetQueryableAsync();

        query = query.Where(x => x.AcademicYearId == academicYearId);

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        if (!search.IsNullOrWhiteSpace())
        {
            var text = search!.Trim().ToLowerInvariant();
            var incomeQuery = await transactionCategoryRepository.GetQueryableAsync();
            var matchingIncomeIds = await AsyncExecuter.ToListAsync(incomeQuery
                .Where(x => x.AcademicYearId == academicYearId && x.CategoryType == TransactionType.Income && x.Name.ToLower().Contains(text))
                .Select(x => x.Id));
            // Accounting links live in ABP ExtraProperties and cannot be translated as CLR properties.
            // Resolve matching category IDs within the already year/status-filtered tenant query, before pagination.
            var yearCategories = await AsyncExecuter.ToListAsync(query);
            var categoryIds = yearCategories.Where(x => x.IncomeCategoryId.HasValue && matchingIncomeIds.Contains(x.IncomeCategoryId.Value))
                .Select(x => x.Id).ToArray();
            query = query.Where(x => x.Name.ToLower().Contains(text) ||
                (x.Description != null && x.Description.ToLower().Contains(text)) || Enumerable.Contains(categoryIds, x.Id));
        }

        query = await ApplyListFiltersAsync(query, tableQuery);
		var totalCount = await AsyncExecuter.CountAsync(query);

        var items = await AsyncExecuter.ToListAsync(
            query
            .OrderByDescending(x => x.CreationTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
        );

		var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
		var labels = await LoadAccountingLabelsAsync(items);
		var responses = items.Select(item => MapResponse(item, labels)).ToList();

        return new PaginatedChargeCategoryResponse
        {
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = totalPages,
			Items = responses
        };
    }

    public async Task<ChargeCategoryResponse> GetAsync(Guid id)
    {
        var chargeCategory = await chargeCategoryRepository.GetAsync(id);
		await financeScopeAccessService.EnsureAcademicContextAccessibleAsync(chargeCategory.AcademicYearId, null, null, null, null);
		return await ToResponseAsync(chargeCategory);
    }

	[UnitOfWork(isTransactional: true)]
    public async Task<ChargeCategoryResponse> UpdateAsync(Guid id, UpdateChargeCategoryRequest input)
    {
        var chargeCategory = await chargeCategoryRepository.GetAsync(id);
		await financeScopeAccessService.EnsureAcademicContextAccessibleAsync(chargeCategory.AcademicYearId, null, null, null, null);
		var (incomeCategory, accountHead) = await ValidateAccountingMappingAsync(
			chargeCategory.AcademicYearId, input.IncomeCategoryId);
		if (await chargeRepository.AnyAsync(x => x.ChargeCategoryId == id) &&
			chargeCategory.IncomeCategoryId != input.IncomeCategoryId)
			throw new BusinessException(FinanceChargeErrorCodes.ChargeCategoryAccountingLocked);

        chargeCategory.Name = input.Name;
        if (input.IsApprovalRequired.HasValue)
        {
            chargeCategory.IsApprovalRequired = input.IsApprovalRequired.Value;
        }
        chargeCategory.Description = input.Description;
		chargeCategory.SetAccountingMapping(incomeCategory.Id, accountHead?.Id);

		// validate for duplicate name within the same academic year
		var existingCategory = await chargeCategoryRepository.AnyAsync(x => x.AcademicYearId == chargeCategory.AcademicYearId && x.Name == input.Name && x.Id != id);

		if (existingCategory)
		{
			throw new BusinessException("A charge category with the same name already exists for this academic year.");
		}

		await chargeCategoryRepository.UpdateAsync(chargeCategory);

		return await ToResponseAsync(chargeCategory);
    }

	[UnitOfWork(isTransactional: true)]
	public async Task<ChargeCategoryResponse> UpdateStatusAsync(Guid id, UpdateChargeCategoryStatusRequest input)
	{
		var category = await chargeCategoryRepository.GetAsync(id);
		await financeScopeAccessService.EnsureAcademicContextAccessibleAsync(category.AcademicYearId, null, null, null, null);
		if (!input.IsActive && string.IsNullOrWhiteSpace(input.Reason))
			throw new UserFriendlyException("A deactivation reason is required.");
		if (input.IsActive)
			await ValidateAccountingMappingAsync(category.AcademicYearId,
				category.IncomeCategoryId ?? Guid.Empty);
		category.SetStatus(input.IsActive, CurrentUser.Id, input.Reason);
		await chargeCategoryRepository.UpdateAsync(category, autoSave: true);
		return await ToResponseAsync(category);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ChargeCategory.Delete)]
	public async Task<ChargeCategoryUsageResponse> GetUsageAsync(Guid id)
	{
		var category = await chargeCategoryRepository.GetAsync(id);
		await financeScopeAccessService.EnsureAcademicContextAccessibleAsync(category.AcademicYearId, null, null, null, null);
		var lines = await GetChargeLinesAsync(id);
		var studentLines = lines.Where(x => x.StudentId.HasValue);
		var usage = new ChargeCategoryUsageResponse
		{
			CategoryId = category.Id,
			CategoryName = category.Name,
			ChargeCount = await GetChargeCountAsync(id, lines),
			StudentAssignmentCount = await AsyncExecuter.CountAsync(studentLines)
		};
		if (usage.StudentAssignmentCount > 0 &&
			await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.View))
		{
			var scopedStudents = await financeScopeAccessService.GetScopedStudentIdsQueryAsync();
			usage.ChargeNames = await AsyncExecuter.ToListAsync(studentLines
				.Where(x => scopedStudents.Contains(x.StudentId!.Value))
				.Select(x => x.ChargeName).Distinct().OrderBy(x => x).Take(10));
		}
		return usage;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ChargeCategory.Delete)]
	[UnitOfWork(isTransactional: true)]
	public async Task DeleteAsync(Guid id)
	{
		// Charge creation takes the same lock before it resolves the category.
		await referenceLock.AcquireAsync();
		var category = await chargeCategoryRepository.GetAsync(id);
		await financeScopeAccessService.EnsureAcademicContextAccessibleAsync(category.AcademicYearId, null, null, null, null);
		var chargeCount = await GetChargeCountAsync(id, await GetChargeLinesAsync(id));
		if (chargeCount > 0)
			throw new BusinessException(FinanceChargeErrorCodes.ChargeCategoryInUse)
				.WithData("CategoryName", category.Name)
				.WithData("ChargeCount", chargeCount);

		await chargeCategoryRepository.DeleteAsync(category, autoSave: true);
	}

	private async Task<IQueryable<Charge>> GetChargeLinesAsync(Guid id) =>
		(await chargeRepository.GetQueryableAsync()).Where(x => x.ChargeCategoryId == id);

	private async Task<int> GetChargeCountAsync(Guid id, IQueryable<Charge> lines)
	{
		var requests = await chargeRequestRepository.GetQueryableAsync();
		return await AsyncExecuter.CountAsync(requests.Where(x => x.ChargeCategoryId == id).Select(x => x.Id)
			.Union(lines.Select(x => x.ChargeRequestId)));
	}

	private async Task<(TransactionCategory Category, AccountHead? AccountHead)> ValidateAccountingMappingAsync(
		Guid academicYearId, Guid incomeCategoryId)
	{
		await referenceLock.AcquireAsync();
		var category = await transactionCategoryRepository.FindAsync(incomeCategoryId);
		if (category is null || category.AcademicYearId != academicYearId ||
			category.CategoryType != TransactionType.Income || !category.IsActive)
			throw new BusinessException(FinanceChargeErrorCodes.ChargeCategoryAccountingInvalid);
		return (category, null);
	}

	private async Task<ChargeCategoryResponse> ToResponseAsync(ChargeCategory category)
	{
		return MapResponse(category, await LoadAccountingLabelsAsync([category]));
	}
}

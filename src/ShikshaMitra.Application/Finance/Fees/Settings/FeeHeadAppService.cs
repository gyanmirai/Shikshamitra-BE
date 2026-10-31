using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp;
using Volo.Abp.Uow;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings.Managers;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;

namespace ShikshaMitra.Finance.Fees.Settings;

[Authorize]
[RemoteService(false)]
public partial class FeeHeadAppService(
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<FeeCategory, Guid> feeCategoryRepository,
	IRepository<FeeStructure, Guid> feeStructureRepository,
	IRepository<TransactionCategory, Guid> incomeCategoryRepository,
	IRepository<AccountHead, Guid> accountHeadRepository,
	FeeCategoryManager feeCategoryManager,
	FeeHeadAccountingManager accountingManager,
	FeeHeadDependencyQuery dependencyQuery
) : ApplicationService, IFeeHeadAppService
{
	[UnitOfWork(isTransactional: true)]
	public async Task<FeeHeadResponse> CreateAsync(Guid academicYearId, FeeHeadCreateRequest input)
    {
		await feeCategoryManager.EnsureFeeHeadNameIsUniqueAsync(academicYearId, input.FeeCategoryId, input.Name);
		await accountingManager.GetValidMappingAsync(academicYearId, input.IncomeCategoryId, creatingReference: true);

		var feeHead = new FeeHead(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            academicYearId,
            input.FeeCategoryId,
            input.Name.Trim(),
            null, // Module selection is retired for new fee heads.
            input.IsConcessionApplied,
            input.Description,
            input.IncomeCategoryId
        );

        await feeHeadRepository.InsertAsync(feeHead);

        return await MapToResponseAsync(feeHead);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeHead.View)]
    public async Task<FeeHeadListResponse> GetListAsync(Guid academicYearId, Guid? feeCategoryId = null, string search = null, int page = 1, int pageSize = 10, string? filters = null)
    {
        var tableQuery = ListFilters.Parse(filters);
        var hasHeaderFilters = tableQuery.Filters.Count > 0;
        var query = await feeHeadRepository.GetQueryableAsync();

        query = query.Where(x => x.AcademicYearId == academicYearId);

        if (feeCategoryId.HasValue)
        {
            query = query.Where(x => x.FeeCategoryId == feeCategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Name.Contains(search));
        }

        var totalCount = await feeHeadRepository.AsyncExecuter.CountAsync(query);

        query = query.OrderByDescending(x => x.CreationTime);
        if (!hasHeaderFilters)
            query = query.Skip((page - 1) * pageSize).Take(pageSize);

        var items = await feeHeadRepository.AsyncExecuter.ToListAsync(query);

        var categoryIds = items.Select(x => x.FeeCategoryId).Distinct().ToList();
        var categories = await feeCategoryRepository.GetListAsync(x => categoryIds.Contains(x.Id));
        var categoryDict = categories.ToDictionary(x => x.Id, x => x.Name);
		var incomeCategoryIds = items.Select(x => x.IncomeCategoryId).Distinct().ToList();
		var incomeCategories = await incomeCategoryRepository.GetListAsync(x => incomeCategoryIds.Contains(x.Id));
		var incomeCategoryDict = incomeCategories.ToDictionary(x => x.Id);
		var accountHeadIds = incomeCategories.Where(x => x.DefaultAccountHeadId.HasValue)
			.Select(x => x.DefaultAccountHeadId!.Value).Distinct().ToList();
		var accountHeads = await accountHeadRepository.GetListAsync(x => accountHeadIds.Contains(x.Id));
		var accountHeadDict = accountHeads.ToDictionary(x => x.Id);

		var dependencies = await dependencyQuery.GetAsync(items.Select(x => x.Id));

        var dtos = items.Select(x =>
        {
            var response = ObjectMapper.Map<FeeHead, FeeHeadResponse>(x);
            response.FeeCategoryName = categoryDict.GetValueOrDefault(x.FeeCategoryId);
			PopulateAccountingMapping(response, x, incomeCategoryDict, accountHeadDict);
            response.Dependencies = dependencies[x.Id];
            response.IsUsedInDownstream = response.Dependencies.IsBlocked;
            return response;
        }).ToList();

        if (hasHeaderFilters)
        {
            dtos = ListFilters.Apply(dtos.AsQueryable(), tableQuery).ToList();
            totalCount = dtos.Count;
            dtos = dtos.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        }

        return new FeeHeadListResponse
        {
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = dtos
        };
    }

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeHead.View)]
	public async Task<List<ActiveFeeHeadLookupDto>> GetActiveAsync(Guid academicYearId)
	{
		var feeHeadQuery = await feeHeadRepository.GetQueryableAsync();
		var categoryQuery = await feeCategoryRepository.GetQueryableAsync();
		var incomeCategoryQuery = await incomeCategoryRepository.GetQueryableAsync();
		var accountHeadQuery = await accountHeadRepository.GetQueryableAsync();

		return await AsyncExecuter.ToListAsync(
			from feeHead in feeHeadQuery
			join category in categoryQuery on feeHead.FeeCategoryId equals category.Id
			join incomeCategory in incomeCategoryQuery on feeHead.IncomeCategoryId equals incomeCategory.Id
			join mappedAccountHead in accountHeadQuery on incomeCategory.DefaultAccountHeadId equals (Guid?)mappedAccountHead.Id into accountHeadGroup
			from accountHead in accountHeadGroup.DefaultIfEmpty()
			where feeHead.AcademicYearId == academicYearId &&
				feeHead.IsActive &&
				category.AcademicYearId == academicYearId &&
				category.IsActive &&
				incomeCategory.AcademicYearId == academicYearId &&
				incomeCategory.CategoryType == TransactionType.Income &&
				incomeCategory.IsActive
			orderby category.Name, feeHead.Name
			select new ActiveFeeHeadLookupDto
			{
				Id = feeHead.Id,
				AcademicYearId = feeHead.AcademicYearId,
				FeeCategoryId = feeHead.FeeCategoryId,
				FeeCategoryName = category.Name,
				IncomeCategoryId = incomeCategory.Id,
				IncomeCategoryName = incomeCategory.Name,
				IncomeAccountHeadId = accountHead == null ? Guid.Empty : accountHead.Id,
				IncomeAccountHeadName = accountHead == null ? string.Empty : accountHead.Name,
				Name = feeHead.Name,
				Module = feeHead.Module,
				IsConcessionApplied = feeHead.IsConcessionApplied
			});
	}

	public async Task<FeeHeadResponse> GetByIdAsync(Guid id)
	{
		var feeHead = await feeHeadRepository.GetAsync(id);
		return await MapToResponseAsync(feeHead);
	}

	[UnitOfWork(isTransactional: true)]
    public async Task<FeeHeadResponse> UpdateAsync(Guid feeHeadId, FeeHeadUpdateRequest input)
    {
		var feeHead = await feeHeadRepository.GetAsync(feeHeadId);

		await feeCategoryManager.EnsureFeeHeadNameIsUniqueAsync(
			feeHead.AcademicYearId, input.FeeCategoryId, input.Name, feeHeadId);
		await accountingManager.GetValidMappingAsync(feeHead.AcademicYearId, input.IncomeCategoryId, creatingReference: true);
		if (feeHead.IncomeCategoryId != input.IncomeCategoryId &&
			(await GetUsedFeeHeadIdsAsync([feeHeadId])).Count > 0)
		{
			throw new BusinessException(FinanceFeeHeadErrorCodes.IncomeCategoryChangeBlocked);
		}

		feeHead.Update(input.FeeCategoryId, input.Name.Trim(), feeHead.Module, input.IsConcessionApplied,
			input.Description, input.IncomeCategoryId);

		await feeHeadRepository.UpdateAsync(feeHead);

        return await MapToResponseAsync(feeHead);
    }

    public async Task<MessageResponse> DeleteAsync(Guid feeHeadId)
    {
		var head = await feeHeadRepository.GetAsync(feeHeadId);
		var dependencies = (await dependencyQuery.GetAsync([feeHeadId]))[feeHeadId];
		if (dependencies.IsBlocked)
			throw FeeHeadDependencyQuery.Blocked(FinanceFeeHeadErrorCodes.CannotDeleteInUse, head.Name, dependencies);

		await feeHeadRepository.DeleteAsync(feeHeadId);

        return new MessageResponse
        {
            Message = "Fee head deleted successfully"
        };
    }

    public async Task<FeeHeadResponse> UpdateStatusAsync(Guid feeHeadId, FeeHeadStatusUpdateRequest input)
    {
        var feeHead = await feeHeadRepository.GetAsync(feeHeadId);

		await feeCategoryManager.EnsureFeeHeadStatusChangeAllowedAsync(feeHead.FeeCategoryId);

		if (input.IsActive)
		{
			await accountingManager.GetValidMappingAsync(feeHead.AcademicYearId, feeHead.IncomeCategoryId);
		}

		if (!input.IsActive)
		{
			var dependencies = (await dependencyQuery.GetAsync([feeHeadId]))[feeHeadId];
			if (dependencies.IsBlocked)
				throw FeeHeadDependencyQuery.Blocked(FinanceFeeHeadErrorCodes.CannotDeactivateInUse, feeHead.Name, dependencies);
		}

		feeHead.SetActiveStatus(input.IsActive, input.DeactivationReason);

		await feeHeadRepository.UpdateAsync(feeHead);

        return await MapToResponseAsync(feeHead);
    }

	private async Task<HashSet<Guid>> GetUsedFeeHeadIdsAsync(List<Guid> feeHeadIds)
	{
		if (feeHeadIds.Count == 0)
		{
			return [];
		}

		return (await dependencyQuery.GetAsync(feeHeadIds)).Where(x => x.Value.IsBlocked).Select(x => x.Key).ToHashSet();
	}

	private async Task<FeeHeadResponse> MapToResponseAsync(FeeHead feeHead)
	{
		var response = ObjectMapper.Map<FeeHead, FeeHeadResponse>(feeHead);

		var category = await feeCategoryRepository.FindAsync(feeHead.FeeCategoryId);
		response.FeeCategoryName = category?.Name;
		var incomeCategory = await incomeCategoryRepository.FindAsync(feeHead.IncomeCategoryId);
		var accountHead = incomeCategory?.DefaultAccountHeadId is { } accountHeadId
			? await accountHeadRepository.FindAsync(accountHeadId) : null;
		response.IncomeCategoryName = incomeCategory?.Name;
		response.IncomeAccountHeadId = accountHead?.Id ?? Guid.Empty;
		response.IncomeAccountHeadName = accountHead?.Name;
		response.Dependencies = (await dependencyQuery.GetAsync([feeHead.Id]))[feeHead.Id];
		response.IsUsedInDownstream = response.Dependencies.IsBlocked;

		return response;
	}

	private static void PopulateAccountingMapping(FeeHeadResponse response, FeeHead feeHead,
		IReadOnlyDictionary<Guid, TransactionCategory> incomeCategories,
		IReadOnlyDictionary<Guid, AccountHead> accountHeads)
	{
		if (!incomeCategories.TryGetValue(feeHead.IncomeCategoryId, out var incomeCategory)) return;
		response.IncomeCategoryName = incomeCategory.Name;
		if (!incomeCategory.DefaultAccountHeadId.HasValue ||
			!accountHeads.TryGetValue(incomeCategory.DefaultAccountHeadId.Value, out var accountHead)) return;
		response.IncomeAccountHeadId = accountHead.Id;
		response.IncomeAccountHeadName = accountHead.Name;
	}
}

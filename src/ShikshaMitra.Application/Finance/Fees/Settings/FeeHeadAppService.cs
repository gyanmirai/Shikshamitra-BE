using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp;
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
public class FeeHeadAppService(
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<FeeCategory, Guid> feeCategoryRepository,
	IRepository<FeeStructure, Guid> feeStructureRepository,
	IRepository<TransactionCategory, Guid> incomeCategoryRepository,
	IRepository<AccountHead, Guid> accountHeadRepository,
	FeeCategoryManager feeCategoryManager,
	FeeHeadAccountingManager accountingManager
) : ApplicationService, IFeeHeadAppService
{
	public async Task<FeeHeadResponse> CreateAsync(Guid academicYearId, FeeHeadCreateRequest input)
    {
		await feeCategoryManager.EnsureFeeHeadNameIsUniqueAsync(academicYearId, input.FeeCategoryId, input.Name);
		await accountingManager.GetValidMappingAsync(academicYearId, input.IncomeCategoryId);

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

    public async Task<FeeHeadListResponse> GetListAsync(Guid academicYearId, Guid? feeCategoryId = null, string search = null, int page = 1, int pageSize = 10)
    {
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

        query = query.OrderByDescending(x => x.CreationTime)
                     .Skip((page - 1) * pageSize)
                     .Take(pageSize);

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

		var usedFeeHeadIds = await GetUsedFeeHeadIdsAsync(items.Select(x => x.Id).ToList());

        var dtos = items.Select(x =>
        {
            var response = ObjectMapper.Map<FeeHead, FeeHeadResponse>(x);
            response.FeeCategoryName = categoryDict.GetValueOrDefault(x.FeeCategoryId);
			PopulateAccountingMapping(response, x, incomeCategoryDict, accountHeadDict);
            response.IsUsedInDownstream = usedFeeHeadIds.Contains(x.Id);
            return response;
        }).ToList();

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

		return await AsyncExecuter.ToListAsync(
			from feeHead in feeHeadQuery
			join category in categoryQuery on feeHead.FeeCategoryId equals category.Id
			join incomeCategory in incomeCategoryQuery on feeHead.IncomeCategoryId equals incomeCategory.Id
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

    public async Task<FeeHeadResponse> UpdateAsync(Guid feeHeadId, FeeHeadUpdateRequest input)
    {
		var feeHead = await feeHeadRepository.GetAsync(feeHeadId);

		await feeCategoryManager.EnsureFeeHeadNameIsUniqueAsync(
			feeHead.AcademicYearId, input.FeeCategoryId, input.Name, feeHeadId);
		await accountingManager.GetValidMappingAsync(feeHead.AcademicYearId, input.IncomeCategoryId);
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
		if ((await GetUsedFeeHeadIdsAsync([feeHeadId])).Count > 0)
		{
			throw new BusinessException(FinanceFeeHeadErrorCodes.CannotDeleteInUse);
		}

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

		if (!input.IsActive && (await GetUsedFeeHeadIdsAsync([feeHeadId])).Count > 0)
		{
			throw new BusinessException(FinanceFeeHeadErrorCodes.CannotDeactivateInUse);
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

		var structureQuery = await feeStructureRepository.GetQueryableAsync();
		var usedIds = await feeStructureRepository.AsyncExecuter.ToListAsync(
			structureQuery.Where(fs => fs.IsLocked)
						  .SelectMany(fs => fs.FeeStructureDetails)
						  .Where(fsd => feeHeadIds.Contains(fsd.FeeHeadId))
						  .Select(fsd => fsd.FeeHeadId)
						  .Distinct()
		);

		return [.. usedIds];
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
		response.IsUsedInDownstream = (await GetUsedFeeHeadIdsAsync([feeHead.Id])).Count > 0;

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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings.Managers;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.Settings;

[Authorize]
[RemoteService(false)]
public class FeeCategoryAppService(
	IRepository<FeeCategory, Guid> feeCategoryRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<FeeStructure, Guid> feeStructureRepository,
	IRepository<TransactionCategory, Guid> incomeCategoryRepository,
	IRepository<AccountHead, Guid> accountHeadRepository,
	FeeCategoryManager feeCategoryManager,
	FeeHeadAccountingManager accountingManager
) : ApplicationService, IFeeCategoryAppService
{
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
			await accountingManager.GetValidMappingAsync(academicYearId, headInput.IncomeCategoryId);
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
				headInput.Module,
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

	public async Task<FeeCategoryListResponse> GetListAsync(Guid academicYearId, string search = null, int page = 1, int pageSize = 10)
	{
		var query = await feeCategoryRepository.GetQueryableAsync();

		query = query.Where(x => x.AcademicYearId == academicYearId);

		if (!string.IsNullOrWhiteSpace(search))
		{
			query = query.Where(x => x.Name.Contains(search));
		}

		var totalCount = await feeCategoryRepository.AsyncExecuter.CountAsync(query);

		query = query.OrderByDescending(x => x.CreationTime)
					 .Skip((page - 1) * pageSize)
					 .Take(pageSize);

		var categories = await feeCategoryRepository.AsyncExecuter.ToListAsync(query);
		var categoryIds = categories.Select(x => x.Id).ToList();

		var headQuery = await feeHeadRepository.GetQueryableAsync();
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

	public async Task<FeeCategoryResponse> GetByIdAsync(Guid id)
	{
		var feeCategory = await feeCategoryRepository.GetAsync(id);
		var feeHeads = await feeHeadRepository.GetListAsync(x => x.FeeCategoryId == id);

		return await MapToResponseAsync(feeCategory, feeHeads);
	}

	public async Task<FeeCategoryResponse> UpdateAsync(Guid id, FeeCategoryUpdateRequest input)
	{
		var feeCategory = await feeCategoryRepository.GetAsync(id);

		await feeCategoryManager.EnsureNameIsUniqueAsync(feeCategory.AcademicYearId, input.Name, id);

		feeCategory.Update(input.Name.Trim(), input.Description);
		await feeCategoryRepository.UpdateAsync(feeCategory);

		var existingHeads = await feeHeadRepository.GetListAsync(x => x.FeeCategoryId == id);
		var existingHeadsById = existingHeads.ToDictionary(x => x.Id);
		var incomingIds = input.FeeHeads.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToHashSet();
		var usedFeeHeadIds = await GetUsedFeeHeadIdsAsync(existingHeads.Select(x => x.Id).ToList());

		var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var resultHeads = new List<FeeHead>();

		foreach (var headInput in input.FeeHeads)
		{
			await accountingManager.GetValidMappingAsync(feeCategory.AcademicYearId, headInput.IncomeCategoryId);
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

				existingHead.Update(id, normalizedName, headInput.Module, headInput.IsConcessionApplied,
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
					headInput.Module,
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
			var removedIds = removedHeads.Select(x => x.Id).ToList();
			var usedIds = usedFeeHeadIds.Where(removedIds.Contains).ToHashSet();
			if (usedIds.Count > 0)
			{
				throw new BusinessException(FinanceFeeHeadErrorCodes.CannotDeleteInUse);
			}

			foreach (var removedHead in removedHeads)
			{
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
			var usedIds = await GetUsedFeeHeadIdsAsync(feeHeads.Select(x => x.Id).ToList());
			if (usedIds.Count > 0)
			{
				throw new BusinessException(FinanceFeeCategoryErrorCodes.CannotDeleteInUse)
					.WithData("Reason", "Cannot deactivate a fee category whose fee heads are in use.");
			}
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

		var structureQuery = await feeStructureRepository.GetQueryableAsync();
		var usedIds = await feeStructureRepository.AsyncExecuter.ToListAsync(
			structureQuery.Where(fs => fs.IsLocked)
						  .SelectMany(fs => fs.FeeStructureDetails)
						  .Where(fsd => feeHeadIds.Contains(fsd.FeeHeadId))
						  .Select(fsd => fsd.FeeHeadId)
						  .Distinct()
		);

		return new HashSet<Guid>(usedIds);
	}

	private async Task<FeeCategoryResponse> MapToResponseAsync(FeeCategory feeCategory, List<FeeHead> feeHeads, HashSet<Guid> usedFeeHeadIds = null)
	{
		usedFeeHeadIds ??= await GetUsedFeeHeadIdsAsync(feeHeads.Select(x => x.Id).ToList());
		var incomeCategoryIds = feeHeads.Select(x => x.IncomeCategoryId).Distinct().ToList();
		var incomeCategories = await incomeCategoryRepository.GetListAsync(x => incomeCategoryIds.Contains(x.Id));
		var incomeCategoryDict = incomeCategories.ToDictionary(x => x.Id);
		var accountHeadIds = incomeCategories.Where(x => x.DefaultAccountHeadId.HasValue)
			.Select(x => x.DefaultAccountHeadId!.Value).Distinct().ToList();
		var accountHeads = await accountHeadRepository.GetListAsync(x => accountHeadIds.Contains(x.Id));
		var accountHeadDict = accountHeads.ToDictionary(x => x.Id);

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
			headResponse.IsUsedInDownstream = usedFeeHeadIds.Contains(feeHead.Id);
			return headResponse;
		}).ToList();
		response.IsUsedInDownstream = feeHeads.Any(x => usedFeeHeadIds.Contains(x.Id));

		return response;
	}
}

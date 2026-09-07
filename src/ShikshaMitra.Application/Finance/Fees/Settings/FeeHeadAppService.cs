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
public class FeeHeadAppService(
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<FeeCategory, Guid> feeCategoryRepository,
	IAcademicYearRepository academicYearRepository,
	FeeCategoryManager feeCategoryManager,
	FeeHeadUsageService usageService,
	FinanceReferenceCacheService referenceCache
) : ApplicationService, IFeeHeadAppService
{
	public async Task<FeeHeadResponse> CreateAsync(Guid academicYearId, FeeHeadCreateRequest input)
	{
		await academicYearRepository.GetAsync(academicYearId);
		var category = await GetCategoryAsync(input.FeeCategoryId, academicYearId);
		await feeCategoryManager.EnsureFeeHeadNameIsUniqueAsync(academicYearId, category.Id, input.Name);
		var head = new FeeHead(GuidGenerator.Create(), CurrentTenant.Id, academicYearId, category.Id,
			input.Name, input.Module, input.IsConcessionApplied, Clean(input.Description));
		await feeHeadRepository.InsertAsync(head);
		await SaveWithDuplicateProtectionAsync(input.Name);
		await referenceCache.InvalidateFeeHeadsAsync();
		return await MapAsync(head, category);
	}

	public async Task<FeeHeadListResponse> GetListAsync(Guid academicYearId, Guid? feeCategoryId = null, string? search = null, bool includeSuperseded = false, bool effectiveOnly = false, int page = 1, int pageSize = 10)
	{
		if (page < 1 || pageSize is < 1 or > 100)
			throw new BusinessException(FinanceFeeCategoryErrorCodes.InvalidQuery).WithData("Page", page).WithData("PageSize", pageSize);
		await academicYearRepository.GetAsync(academicYearId);
		var query = (await feeHeadRepository.GetQueryableAsync()).Where(x => x.AcademicYearId == academicYearId);
		if (!includeSuperseded) query = query.Where(x => x.IsCurrentRevision);
		if (effectiveOnly) query = query.Where(x => x.IsCurrentRevision && x.IsActive && x.FeeCategory.IsActive);
		if (feeCategoryId.HasValue) query = query.Where(x => x.FeeCategoryId == feeCategoryId.Value);
		if (!string.IsNullOrWhiteSpace(search))
		{
			var normalized = FeeCategoryManager.NormalizeName(search);
			query = query.Where(x => x.NormalizedName.Contains(normalized) ||
				(x.Description != null && x.Description.ToUpper().Contains(normalized)));
		}
		var totalCount = await feeHeadRepository.AsyncExecuter.CountAsync(query);
		var heads = await feeHeadRepository.AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.IsCurrentRevision)
			.ThenBy(x => x.Name).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize));
		var categoryIds = heads.Select(x => x.FeeCategoryId).Distinct().ToList();
		var categories = await feeCategoryRepository.GetListAsync(x => categoryIds.Contains(x.Id));
		var usage = await usageService.GetUsageAsync(heads.Select(x => x.Id));
		return new FeeHeadListResponse
		{
			TotalCount = totalCount, CurrentPage = page, PageSize = pageSize,
			TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
			Items = heads.Select(x => Map(x, categories.Single(c => c.Id == x.FeeCategoryId), usage.GetValueOrDefault(x.Id) ?? [])).ToList()
		};
	}

	public async Task<FeeHeadResponse> GetByIdAsync(Guid id)
	{
		var head = await feeHeadRepository.GetAsync(id);
		return await MapAsync(head, await feeCategoryRepository.GetAsync(head.FeeCategoryId));
	}

	public async Task<FeeHeadResponse> UpdateAsync(Guid feeHeadId, FeeHeadUpdateRequest input)
	{
		var head = await feeHeadRepository.GetAsync(feeHeadId);
		EnsureConcurrency(head, input.ConcurrencyStamp);
		if (!head.IsCurrentRevision) throw new BusinessException(FinanceFeeHeadErrorCodes.RevisionRequired);
		var category = await GetCategoryAsync(input.FeeCategoryId, head.AcademicYearId);
		var usage = await usageService.GetUsageAsync([head.Id]);
		var identityChanged = head.FeeCategoryId != input.FeeCategoryId || head.Name != FeeSetupNameNormalizer.ForStorage(input.Name) ||
			head.Module != input.Module || head.IsConcessionApplied != input.IsConcessionApplied;
		if (identityChanged && usage[head.Id].Count > 0)
			throw new BusinessException(FinanceFeeHeadErrorCodes.RevisionRequired).WithData("Blockers", usage[head.Id]);
		await feeCategoryManager.EnsureFeeHeadNameIsUniqueAsync(head.AcademicYearId, category.Id, input.Name, head.Id);
		head.Update(category.Id, input.Name, input.Module, input.IsConcessionApplied, Clean(input.Description));
		await feeHeadRepository.UpdateAsync(head);
		await SaveWithDuplicateProtectionAsync(input.Name);
		await referenceCache.InvalidateFeeHeadsAsync();
		return Map(head, category, usage[head.Id]);
	}

	public async Task<FeeHeadResponse> CreateRevisionAsync(Guid feeHeadId, FeeHeadRevisionRequest input)
	{
		var current = await feeHeadRepository.GetAsync(feeHeadId);
		EnsureConcurrency(current, input.ConcurrencyStamp);
		if (!current.IsCurrentRevision) throw new BusinessException(FinanceFeeHeadErrorCodes.RevisionRequired);
		var identityChanged = current.FeeCategoryId != input.FeeCategoryId ||
			current.Name != FeeSetupNameNormalizer.ForStorage(input.Name) ||
			current.Module != input.Module ||
			current.IsConcessionApplied != input.IsConcessionApplied;
		var usage = await usageService.GetUsageAsync([current.Id]);
		if (!identityChanged || usage[current.Id].Count == 0)
			throw new BusinessException(FinanceFeeHeadErrorCodes.RevisionNotRequired);
		var category = await GetCategoryAsync(input.FeeCategoryId, current.AcademicYearId);
		await feeCategoryManager.EnsureFeeHeadNameIsUniqueAsync(current.AcademicYearId, category.Id, input.Name, current.Id);
		var replacementId = GuidGenerator.Create();
		var replacement = new FeeHead(replacementId, CurrentTenant.Id, current.AcademicYearId, category.Id,
			input.Name, input.Module, input.IsConcessionApplied, Clean(input.Description), current.IsActive,
			current.RevisionGroupId, current.RevisionNumber + 1, current.Id);
		replacement.SetRevisionReason(Clean(input.Reason));
		current.Supersede(replacementId);
		await feeHeadRepository.UpdateAsync(current);
		await feeHeadRepository.InsertAsync(replacement);
		await SaveWithDuplicateProtectionAsync(input.Name);
		await referenceCache.InvalidateFeeHeadsAsync();
		return await MapAsync(replacement, category);
	}

	public async Task<FeeHeadImpactResponse> GetImpactAsync(Guid feeHeadId)
	{
		await feeHeadRepository.GetAsync(feeHeadId);
		var usage = await usageService.GetUsageAsync([feeHeadId]);
		return new FeeHeadImpactResponse { FeeHeadId = feeHeadId, IsUsed = usage[feeHeadId].Count > 0, Blockers = usage[feeHeadId] };
	}

	public async Task<MessageResponse> DeleteAsync(Guid feeHeadId)
	{
		var head = await feeHeadRepository.GetAsync(feeHeadId);
		var usage = await usageService.GetUsageAsync([feeHeadId]);
		if (usage[feeHeadId].Count > 0)
			throw new BusinessException(FinanceFeeHeadErrorCodes.CannotDeleteInUse).WithData("Blockers", usage[feeHeadId]);
		var category = await feeCategoryRepository.GetAsync(head.FeeCategoryId);
		var currentCount = await feeHeadRepository.CountAsync(x => x.FeeCategoryId == category.Id && x.IsCurrentRevision && x.Id != feeHeadId);
		if (category.IsActive && head.IsCurrentRevision && currentCount == 0)
			throw new BusinessException(FinanceFeeHeadErrorCodes.LastHeadCannotBeDeleted);
		await feeHeadRepository.DeleteAsync(head);
		await referenceCache.InvalidateFeeHeadsAsync();
		return new MessageResponse { Message = "Fee head deleted successfully" };
	}

	public async Task<FeeHeadResponse> UpdateStatusAsync(Guid feeHeadId, FeeHeadStatusUpdateRequest input)
	{
		var head = await feeHeadRepository.GetAsync(feeHeadId);
		EnsureConcurrency(head, input.ConcurrencyStamp);
		if (input.IsActive && !head.IsCurrentRevision) throw new BusinessException(FinanceFeeHeadErrorCodes.RevisionRequired);
		head.SetActiveStatus(input.IsActive, Clean(input.DeactivationReason));
		await feeHeadRepository.UpdateAsync(head, autoSave: true);
		return await MapAsync(head, await feeCategoryRepository.GetAsync(head.FeeCategoryId));
	}

	private async Task<FeeCategory> GetCategoryAsync(Guid id, Guid academicYearId)
	{
		var category = await feeCategoryRepository.GetAsync(id);
		if (category.AcademicYearId != academicYearId) throw new BusinessException(FinanceFeeCategoryErrorCodes.NotFound);
		return category;
	}

	private async Task<FeeHeadResponse> MapAsync(FeeHead head, FeeCategory category)
	{
		var usage = await usageService.GetUsageAsync([head.Id]);
		return Map(head, category, usage[head.Id]);
	}

	private FeeHeadResponse Map(FeeHead head, FeeCategory category, System.Collections.Generic.List<string> blockers)
	{
		var response = ObjectMapper.Map<FeeHead, FeeHeadResponse>(head);
		response.FeeCategoryName = category.Name;
		response.ConcurrencyStamp = head.ConcurrencyStamp;
		response.UsageBlockers = blockers;
		response.IsUsedInDownstream = blockers.Count > 0;
		response.IsEffectivelyActive = category.IsActive && head.IsActive && head.IsCurrentRevision;
		return response;
	}

	private static void EnsureConcurrency(FeeHead head, string supplied)
	{
		if (!string.Equals(head.ConcurrencyStamp, supplied, StringComparison.Ordinal))
			throw new BusinessException(FinanceFeeHeadErrorCodes.ConcurrencyConflict);
	}

	private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

	private async Task SaveWithDuplicateProtectionAsync(string name)
	{
		try
		{
			await CurrentUnitOfWork.SaveChangesAsync();
		}
		catch (Exception exception) when (FeeSetupPersistenceErrors.IsFeeHeadNameConflict(exception))
		{
			throw new BusinessException(FinanceFeeCategoryErrorCodes.DuplicateFeeHeadName).WithData("Name", name);
		}
	}
}

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

namespace ShikshaMitra.Finance.Fees.Settings;

[Authorize]
[RemoteService(false)]
public class FeeHeadAppService(
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<FeeCategory, Guid> feeCategoryRepository,
	IRepository<FeeStructure, Guid> feeStructureRepository,
	FeeCategoryManager feeCategoryManager
) : ApplicationService, IFeeHeadAppService
{
	public async Task<FeeHeadResponse> CreateAsync(Guid academicYearId, FeeHeadCreateRequest input)
    {
		await feeCategoryManager.EnsureFeeHeadNameIsUniqueAsync(academicYearId, input.FeeCategoryId, input.Name);

		var feeHead = new FeeHead(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            academicYearId,
            input.FeeCategoryId,
            input.Name.Trim(),
            input.Module,
            input.IsConcessionApplied,
            input.Description
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

		var usedFeeHeadIds = await GetUsedFeeHeadIdsAsync(items.Select(x => x.Id).ToList());

        var dtos = items.Select(x =>
        {
            var response = ObjectMapper.Map<FeeHead, FeeHeadResponse>(x);
            response.FeeCategoryName = categoryDict.GetValueOrDefault(x.FeeCategoryId);
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

		return await AsyncExecuter.ToListAsync(
			from feeHead in feeHeadQuery
			join category in categoryQuery on feeHead.FeeCategoryId equals category.Id
			where feeHead.AcademicYearId == academicYearId &&
				feeHead.IsActive &&
				category.AcademicYearId == academicYearId &&
				category.IsActive
			orderby category.Name, feeHead.Name
			select new ActiveFeeHeadLookupDto
			{
				Id = feeHead.Id,
				AcademicYearId = feeHead.AcademicYearId,
				FeeCategoryId = feeHead.FeeCategoryId,
				FeeCategoryName = category.Name,
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

		feeHead.Update(input.FeeCategoryId, input.Name.Trim(), input.Module, input.IsConcessionApplied, input.Description);

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
		response.IsUsedInDownstream = (await GetUsedFeeHeadIdsAsync([feeHead.Id])).Count > 0;

		return response;
	}
}

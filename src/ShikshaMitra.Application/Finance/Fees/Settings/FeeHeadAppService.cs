using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using ShikshaMitra.Domain.Finance.Fees.Settings;

namespace ShikshaMitra.Finance.Fees.Settings;

[Authorize]
[RemoteService(false)]
public class FeeHeadAppService(
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<FeeCategory, Guid> feeCategoryRepository,
	IRepository<FeeStructure, Guid> feeStructureRepository
) : ApplicationService, IFeeHeadAppService
{
	public async Task<MessageResponse> CreateAsync(Guid academicYearId, FeeHeadCreateRequest input)
    {
		// validation to ensure no duplicate fee head name exists under the same fee category for the same academic year
		var existingFeeHead = await feeHeadRepository.FirstOrDefaultAsync(x =>
			x.AcademicYearId == academicYearId &&
			x.FeeCategoryId == input.FeeCategoryId &&
			x.Name == input.Name
		);

		if (existingFeeHead != null)
		{
			throw new BusinessException("A fee head with the same name already exists under this fee category for the selected academic year.");
		}

		var feeHead = new FeeHead(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            academicYearId,
            input.FeeCategoryId,
            input.Name,
            input.Description
        );

        await feeHeadRepository.InsertAsync(feeHead);

        return new MessageResponse
        {
            Message = "Fee head created successfully"
        };
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

        // Fetch fee category names
        var categoryIds = items.Select(x => x.FeeCategoryId).Distinct().ToList();
        var categories = await feeCategoryRepository.GetListAsync(x => categoryIds.Contains(x.Id));
        var categoryDict = categories.ToDictionary(x => x.Id, x => x.Name);

        var dtos = items.Select(x => new FeeHeadResponse
        {
            Id = x.Id,
            AcademicYearId = x.AcademicYearId,
            FeeCategoryId = x.FeeCategoryId,
            FeeCategoryName = categoryDict.ContainsKey(x.FeeCategoryId) ? categoryDict[x.FeeCategoryId] : null,
            Name = x.Name,
            Description = x.Description,
            IsActive = x.IsActive,
            CreatedAt = x.CreationTime,
            UpdatedAt = x.LastModificationTime
        }).ToList();

		var structureQuery = await feeStructureRepository.GetQueryableAsync();
		var itemIds = dtos.Select(x => x.Id).ToList();
		var usedFeeHeadIds = new HashSet<Guid>(
			await feeStructureRepository.AsyncExecuter.ToListAsync(
				structureQuery.SelectMany(fs => fs.FeeStructureDetails)
							  .Where(fsd => itemIds.Contains(fsd.FeeHeadId))
							  .Select(fsd => fsd.FeeHeadId)
							  .Distinct()
			)
		);

		foreach (var item in dtos)
		{
			item.IsUsedInDownstream = usedFeeHeadIds.Contains(item.Id);
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

    public async Task<MessageResponse> UpdateAsync(Guid feeHeadId, FeeHeadUpdateRequest input)
    {
		var feeHead = await feeHeadRepository.GetAsync(feeHeadId);

		// validation to ensure no duplicate fee head name exists under the same fee category for the same academic year
		var existingFeeHead = await feeHeadRepository.FirstOrDefaultAsync(x =>
			x.Id != feeHeadId &&
			x.AcademicYearId == feeHead.AcademicYearId &&
			x.FeeCategoryId == input.FeeCategoryId &&
			x.Name == input.Name
		);

		if (existingFeeHead != null)
		{
			throw new BusinessException("A fee head with the same name already exists under this fee category for the selected academic year.");
		}

		feeHead.Update(input.FeeCategoryId, input.Name, input.Description);

		await feeHeadRepository.UpdateAsync(feeHead);

        return new MessageResponse
        {
            Message = "Fee head updated successfully"
        };
    }

    public async Task<MessageResponse> DeleteAsync(Guid feeHeadId)
    {
		// check if fee head is associated with any fee structure details before deleting
		var isAssociatedWithFeeStructure = await feeStructureRepository.AnyAsync(fs => fs.FeeStructureDetails.Any(fsd => fsd.FeeHeadId == feeHeadId) && fs.IsLocked);

		if (isAssociatedWithFeeStructure)
		{
			throw new BusinessException("Cannot delete fee head as it is associated with existing fee structures.");
		}

		await feeHeadRepository.DeleteAsync(feeHeadId);

        return new MessageResponse
        {
            Message = "Fee head deleted successfully"
        };
    }

    public async Task<MessageResponse> UpdateStatusAsync(Guid feeHeadId, FeeHeadStatusUpdateRequest input)
    {
        var feeHead = await feeHeadRepository.GetAsync(feeHeadId);

        feeHead.SetActiveStatus(input.IsActive);

		// if deactivating the fee head, check if it is associated with any fee structure details before updating status
		if (!input.IsActive)
		{
			var isAssociatedWithFeeStructure = await feeStructureRepository.AnyAsync(fs => fs.FeeStructureDetails.Any(fsd => fsd.FeeHeadId == feeHeadId) && fs.IsLocked);
			if (isAssociatedWithFeeStructure)
			{
				throw new BusinessException("Cannot deactivate fee head as it is associated with existing fee structures.");
			}
		}

		await feeHeadRepository.UpdateAsync(feeHead);

        return new MessageResponse
        {
            Message = "Fee head status updated successfully"
        };
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.Settings;

[Authorize]
[RemoteService(false)]
public class FeeCategoryAppService(
	IRepository<FeeCategory, Guid> feeCategoryRepository,
	IRepository<FeeHead, Guid> feeHeadRepository
) : ApplicationService, IFeeCategoryAppService
{
    public async Task<MessageResponse> CreateAsync(Guid academicYearId, FeeCategoryCreateRequest input)
    {
		// validation to ensure no duplicate fee category name exists for the same academic year
		var existingCategory = await feeCategoryRepository.FirstOrDefaultAsync(x => x.AcademicYearId == academicYearId && x.Name == input.Name);

		if (existingCategory != null)
		{
			throw new BusinessException("A fee category with the same name already exists for this academic year.");
		}

		var feeCategory = new FeeCategory(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            academicYearId,
            input.Name,
            input.Description
        );

        await feeCategoryRepository.InsertAsync(feeCategory);

        return new MessageResponse
        {
            Message = "Fee category created successfully"
        };
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

        var items = await feeCategoryRepository.AsyncExecuter.ToListAsync(query);

        var dtos = items.Select(x => new FeeCategoryResponse
        {
            Id = x.Id,
            AcademicYearId = x.AcademicYearId,
            Name = x.Name,
            Description = x.Description,
            CreatedAt = x.CreationTime,
            UpdatedAt = x.LastModificationTime
		}).ToList();

		var headQuery = await feeHeadRepository.GetQueryableAsync();
		var itemIds = dtos.Select(x => x.Id).ToList();
		var usedCategoryIds = new HashSet<Guid>(
			await feeHeadRepository.AsyncExecuter.ToListAsync(
				headQuery.Where(x => itemIds.Contains(x.FeeCategoryId))
						 .Select(x => x.FeeCategoryId)
						 .Distinct()
			)
		);

		foreach (var d in dtos)
		{
			d.IsUsedInDownstream = usedCategoryIds.Contains(d.Id);
		}

        return new FeeCategoryListResponse
        {
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = dtos
        };
    }

    public async Task<MessageResponse> UpdateAsync(Guid feeCategoryId, FeeCategoryUpdateRequest input)
    {
		var feeCategory = await feeCategoryRepository.GetAsync(feeCategoryId);

		// validation to ensure no duplicate fee category name exists for the same academic year
		var existingCategory = await feeCategoryRepository.FirstOrDefaultAsync(x => x.AcademicYearId == feeCategory.AcademicYearId && x.Name == input.Name && x.Id != feeCategoryId);

		if (existingCategory != null)
		{
			throw new BusinessException("A fee category with the same name already exists for this academic year.");
		}

		feeCategory.Update(input.Name, input.Description);

		await feeCategoryRepository.UpdateAsync(feeCategory);

        return new MessageResponse
        {
            Message = "Fee category updated successfully"
        };
    }

    public async Task<MessageResponse> DeleteAsync(Guid feeCategoryId)
    {
        await feeCategoryRepository.DeleteAsync(feeCategoryId);

        return new MessageResponse
        {
            Message = "Fee category deleted successfully"
        };
    }
}

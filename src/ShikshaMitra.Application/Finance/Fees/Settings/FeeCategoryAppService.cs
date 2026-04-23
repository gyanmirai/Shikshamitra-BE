using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using ShikshaMitra.Finance.Setting;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.Settings;

[Authorize]
[RemoteService(false)]
public class FeeCategoryAppService : ApplicationService, IFeeCategoryAppService
{
    private readonly IRepository<FeeCategory, Guid> _feeCategoryRepository;

    public FeeCategoryAppService(IRepository<FeeCategory, Guid> feeCategoryRepository)
    {
        _feeCategoryRepository = feeCategoryRepository;
    }

    public async Task<MessageResponse> CreateAsync(Guid academicYearId, FeeCategoryCreateRequest input)
    {
        var feeCategory = new FeeCategory(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            academicYearId,
            input.Name,
            input.Description
        );

        await _feeCategoryRepository.InsertAsync(feeCategory);

        return new MessageResponse
        {
            Message = "Fee category created successfully"
        };
    }

    public async Task<FeeCategoryListResponse> GetListAsync(Guid academicYearId, string search = null, int page = 1, int pageSize = 10)
    {
        var query = await _feeCategoryRepository.GetQueryableAsync();

        query = query.Where(x => x.AcademicYearId == academicYearId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Name.Contains(search));
        }

        var totalCount = await _feeCategoryRepository.AsyncExecuter.CountAsync(query);

        query = query.OrderByDescending(x => x.CreationTime)
                     .Skip((page - 1) * pageSize)
                     .Take(pageSize);

        var items = await _feeCategoryRepository.AsyncExecuter.ToListAsync(query);

        var dtos = items.Select(x => new FeeCategoryResponse
        {
            Id = x.Id,
            AcademicYearId = x.AcademicYearId,
            Name = x.Name,
            Description = x.Description,
            CreatedAt = x.CreationTime,
            UpdatedAt = x.LastModificationTime
        }).ToList();

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
        var feeCategory = await _feeCategoryRepository.GetAsync(feeCategoryId);

        feeCategory.Update(input.Name, input.Description);

        await _feeCategoryRepository.UpdateAsync(feeCategory);

        return new MessageResponse
        {
            Message = "Fee category updated successfully"
        };
    }

    public async Task<MessageResponse> DeleteAsync(Guid feeCategoryId)
    {
        await _feeCategoryRepository.DeleteAsync(feeCategoryId);

        return new MessageResponse
        {
            Message = "Fee category deleted successfully"
        };
    }
}

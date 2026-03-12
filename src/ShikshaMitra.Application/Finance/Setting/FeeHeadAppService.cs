using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Application.Contracts.Finance.Setting.FeeHead;
using ShikshaMitra.Application.Contracts.Finance.Setting.FeeHead.Dtos;
using ShikshaMitra.Application.Contracts.Finance.Setting.FeeCategory.Dtos; // For MessageResponse
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp;

namespace ShikshaMitra.Finance.Setting;

[Authorize]
[RemoteService(false)]
public class FeeHeadAppService : ApplicationService, IFeeHeadAppService
{
    private readonly IRepository<FeeHead, Guid> _feeHeadRepository;
    private readonly IRepository<FeeCategory, Guid> _feeCategoryRepository;

    public FeeHeadAppService(
        IRepository<FeeHead, Guid> feeHeadRepository,
        IRepository<FeeCategory, Guid> feeCategoryRepository)
    {
        _feeHeadRepository = feeHeadRepository;
        _feeCategoryRepository = feeCategoryRepository;
    }

    public async Task<MessageResponse> CreateAsync(Guid academicYearId, FeeHeadCreateRequest input)
    {
        var feeHead = new FeeHead(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            academicYearId,
            input.FeeCategoryId,
            input.Name,
            input.Description
        );

        await _feeHeadRepository.InsertAsync(feeHead);

        return new MessageResponse
        {
            Message = "Fee head created successfully"
        };
    }

    public async Task<FeeHeadListResponse> GetListAsync(Guid academicYearId, Guid? feeCategoryId = null, string search = null, int page = 1, int pageSize = 10)
    {
        var query = await _feeHeadRepository.GetQueryableAsync();

        query = query.Where(x => x.AcademicYearId == academicYearId);

        if (feeCategoryId.HasValue)
        {
            query = query.Where(x => x.FeeCategoryId == feeCategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Name.Contains(search));
        }

        var totalCount = await _feeHeadRepository.AsyncExecuter.CountAsync(query);

        query = query.OrderByDescending(x => x.CreationTime)
                     .Skip((page - 1) * pageSize)
                     .Take(pageSize);

        var items = await _feeHeadRepository.AsyncExecuter.ToListAsync(query);

        // Fetch fee category names
        var categoryIds = items.Select(x => x.FeeCategoryId).Distinct().ToList();
        var categories = await _feeCategoryRepository.GetListAsync(x => categoryIds.Contains(x.Id));
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
        var feeHead = await _feeHeadRepository.GetAsync(feeHeadId);

        feeHead.Update(input.FeeCategoryId, input.Name, input.Description);

        await _feeHeadRepository.UpdateAsync(feeHead);

        return new MessageResponse
        {
            Message = "Fee head updated successfully"
        };
    }

    public async Task<MessageResponse> DeleteAsync(Guid feeHeadId)
    {
        await _feeHeadRepository.DeleteAsync(feeHeadId);

        return new MessageResponse
        {
            Message = "Fee head deleted successfully"
        };
    }

    public async Task<MessageResponse> UpdateStatusAsync(Guid feeHeadId, FeeHeadStatusUpdateRequest input)
    {
        var feeHead = await _feeHeadRepository.GetAsync(feeHeadId);

        feeHead.SetActiveStatus(input.IsActive);

        await _feeHeadRepository.UpdateAsync(feeHead);

        return new MessageResponse
        {
            Message = "Fee head status updated successfully"
        };
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.Finance.Setting.IncomeCategory;
using ShikshaMitra.Application.Contracts.Finance.Setting.IncomeCategory.Dtos;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
namespace ShikshaMitra.Finance.Setting;

[RemoteService(false)]
public class IncomeCategoryAppService : ApplicationService, IIncomeCategoryAppService
{
    private readonly IRepository<IncomeCategory, Guid> _incomeCategoryRepository;

    public IncomeCategoryAppService(IRepository<IncomeCategory, Guid> incomeCategoryRepository)
    {
        _incomeCategoryRepository = incomeCategoryRepository;
    }

    public async Task<IncomeCategoryResponse> CreateAsync(Guid academicYearId, CreateIncomeCategoryRequest input)
    {
        var tags = input.Tags != null && input.Tags.Any() ? string.Join(",", input.Tags) : null;

        var incomeCategory = new IncomeCategory(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            academicYearId,
            input.Name,
            input.Description,
            tags
        );

        await _incomeCategoryRepository.InsertAsync(incomeCategory);

        return MapToResponse(incomeCategory);
    }

    public async Task<PaginatedIncomeCategoryResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null)
    {
        var query = await _incomeCategoryRepository.GetQueryableAsync();
        
        query = query.Where(x => x.AcademicYearId == academicYearId);

        if (!search.IsNullOrWhiteSpace())
        {
            query = query.Where(x => x.Name.Contains(search));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        
        var items = await AsyncExecuter.ToListAsync(
            query
            .OrderByDescending(x => x.CreationTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
        );

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PaginatedIncomeCategoryResponse
        {
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            Items = items.Select(MapToResponse).ToList()
        };
    }

    public async Task<IncomeCategoryResponse> GetAsync(Guid id)
    {
        var incomeCategory = await _incomeCategoryRepository.GetAsync(id);
        return MapToResponse(incomeCategory);
    }

    public async Task<IncomeCategoryResponse> UpdateAsync(Guid id, UpdateIncomeCategoryRequest input)
    {
        var incomeCategory = await _incomeCategoryRepository.GetAsync(id);

        incomeCategory.Name = input.Name;
        incomeCategory.Description = input.Description;
        incomeCategory.Tags = input.Tags != null && input.Tags.Any() ? string.Join(",", input.Tags) : null;

        await _incomeCategoryRepository.UpdateAsync(incomeCategory);

        return MapToResponse(incomeCategory);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _incomeCategoryRepository.DeleteAsync(id);
    }

    private IncomeCategoryResponse MapToResponse(IncomeCategory entity)
    {
        return new IncomeCategoryResponse
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Tags = !string.IsNullOrWhiteSpace(entity.Tags) ? entity.Tags.Split(',').ToList() : new List<string>()
        };
    }
}

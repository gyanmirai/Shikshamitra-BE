using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.Finance.Setting.ExpenseCategory;
using ShikshaMitra.Application.Contracts.Finance.Setting.ExpenseCategory.Dtos;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Setting;

[RemoteService(false)]
public class ExpenseCategoryAppService : ApplicationService, IExpenseCategoryAppService
{
    private readonly IRepository<ExpenseCategory, Guid> _expenseCategoryRepository;

    public ExpenseCategoryAppService(IRepository<ExpenseCategory, Guid> expenseCategoryRepository)
    {
        _expenseCategoryRepository = expenseCategoryRepository;
    }

    public async Task<ExpenseCategoryResponse> CreateAsync(Guid academicYearId, CreateExpenseCategoryRequest input)
    {
        var tags = input.Tags != null && input.Tags.Any() ? string.Join(",", input.Tags) : null;

        var expenseCategory = new ExpenseCategory(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            academicYearId,
            input.Name,
            input.Description,
            tags
        );

        await _expenseCategoryRepository.InsertAsync(expenseCategory);

        return MapToResponse(expenseCategory);
    }

    public async Task<PaginatedExpenseCategoryResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null)
    {
        var query = await _expenseCategoryRepository.GetQueryableAsync();

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

        return new PaginatedExpenseCategoryResponse
        {
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            Items = items.Select(MapToResponse).ToList()
        };
    }

    public async Task<ExpenseCategoryResponse> GetAsync(Guid id)
    {
        var expenseCategory = await _expenseCategoryRepository.GetAsync(id);
        return MapToResponse(expenseCategory);
    }

    public async Task<ExpenseCategoryResponse> UpdateAsync(Guid id, UpdateExpenseCategoryRequest input)
    {
        var expenseCategory = await _expenseCategoryRepository.GetAsync(id);

        expenseCategory.Name = input.Name;
        expenseCategory.Description = input.Description;
        expenseCategory.Tags = input.Tags != null && input.Tags.Any() ? string.Join(",", input.Tags) : null;

        await _expenseCategoryRepository.UpdateAsync(expenseCategory);

        return MapToResponse(expenseCategory);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _expenseCategoryRepository.DeleteAsync(id);
    }

    private ExpenseCategoryResponse MapToResponse(ExpenseCategory entity)
    {
        return new ExpenseCategoryResponse
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Tags = !string.IsNullOrWhiteSpace(entity.Tags) ? entity.Tags.Split(',').ToList() : new List<string>()
        };
    }
}

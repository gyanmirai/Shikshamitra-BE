using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.Finance.Setting;
using ShikshaMitra.Finance.Accounts.Settings.TransactionCategory;
using ShikshaMitra.Finance.Accounts.Settings.TransactionCategory.Dtos;

namespace ShikshaMitra.Finance.Accounts.Settings;

[RemoteService(false)]
public class TransactionCategoryAppService : ApplicationService, ITransactionCategoryAppService
{
    private readonly IRepository<TransactionCategory, Guid> _transactionCategoryRepository;

    public TransactionCategoryAppService(IRepository<TransactionCategory, Guid> transactionCategoryRepository)
    {
        _transactionCategoryRepository = transactionCategoryRepository;
    }

    public async Task<TransactionCategoryResponse> CreateAsync(Guid academicYearId, CreateTransactionCategoryRequest input)
    {
        var tags = input.Tags != null && input.Tags.Any() ? string.Join(",", input.Tags) : null;

        var incomeCategory = new TransactionCategory(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            academicYearId,
            input.Name,
            input.Description,
            tags
        );

        await _transactionCategoryRepository.InsertAsync(incomeCategory);

        return MapToResponse(incomeCategory);
    }

    public async Task<PaginatedTransactionCategoryResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null)
    {
        var query = await _transactionCategoryRepository.GetQueryableAsync();

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

        return new PaginatedTransactionCategoryResponse
        {
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            Items = items.Select(MapToResponse).ToList()
        };
    }

    public async Task<TransactionCategoryResponse> GetAsync(Guid id)
    {
        var incomeCategory = await _transactionCategoryRepository.GetAsync(id);
        return MapToResponse(incomeCategory);
    }

    public async Task<TransactionCategoryResponse> UpdateAsync(Guid id, UpdateTransactionCategoryRequest input)
    {
        var incomeCategory = await _transactionCategoryRepository.GetAsync(id);

        incomeCategory.Name = input.Name;
        incomeCategory.Description = input.Description;
        incomeCategory.Tags = input.Tags != null && input.Tags.Any() ? string.Join(",", input.Tags) : null;

        await _transactionCategoryRepository.UpdateAsync(incomeCategory);

        return MapToResponse(incomeCategory);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _transactionCategoryRepository.DeleteAsync(id);
    }

    private TransactionCategoryResponse MapToResponse(TransactionCategory entity)
    {
        return new TransactionCategoryResponse
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Tags = !string.IsNullOrWhiteSpace(entity.Tags) ? entity.Tags.Split(',').ToList() : new List<string>()
        };
    }
}

using System;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Accounts.Settings.TransactionCategories.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Accounts.Settings;

public interface ITransactionCategoryAppService : IApplicationService
{
    Task<TransactionCategoryResponse> CreateAsync(Guid academicYearId, CreateTransactionCategoryRequest input);
    Task<PaginatedTransactionCategoryResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null);
    Task<TransactionCategoryResponse> GetAsync(Guid id);
    Task<TransactionCategoryResponse> UpdateAsync(Guid id, UpdateTransactionCategoryRequest input);
    Task DeleteAsync(Guid id);
}

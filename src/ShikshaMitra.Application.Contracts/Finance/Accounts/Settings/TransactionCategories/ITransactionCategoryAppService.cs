using System;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Accounts.Settings.TransactionCategories.Dtos;
using ShikshaMitra.Finance.Accounts.Transactions;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Accounts.Settings;

public interface ITransactionCategoryAppService : IApplicationService
{
    Task<TransactionCategoryResponse> CreateAsync(Guid academicYearId, CreateTransactionCategoryRequest input, TransactionType categoryType);
    Task<PaginatedTransactionCategoryResponse> GetListAsync(Guid academicYearId, TransactionType categoryType, int page = 1, int pageSize = 10, string? search = null);
    Task<TransactionCategoryResponse> GetAsync(Guid id, TransactionType categoryType);
    Task<TransactionCategoryResponse> UpdateAsync(Guid id, UpdateTransactionCategoryRequest input, TransactionType categoryType);
    Task DeleteAsync(Guid id, TransactionType categoryType);
}

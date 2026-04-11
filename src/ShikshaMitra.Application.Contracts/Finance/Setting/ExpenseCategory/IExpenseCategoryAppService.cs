using System;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.Finance.Setting.ExpenseCategory.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Application.Contracts.Finance.Setting.ExpenseCategory;

public interface IExpenseCategoryAppService : IApplicationService
{
    Task<ExpenseCategoryResponse> CreateAsync(Guid academicYearId, CreateExpenseCategoryRequest input);
    Task<PaginatedExpenseCategoryResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null);
    Task<ExpenseCategoryResponse> GetAsync(Guid id);
    Task<ExpenseCategoryResponse> UpdateAsync(Guid id, UpdateExpenseCategoryRequest input);
    Task DeleteAsync(Guid id);
}

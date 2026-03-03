using System;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.Finance.Setting.IncomeCategory.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Application.Contracts.Finance.Setting.IncomeCategory;

public interface IIncomeCategoryAppService : IApplicationService
{
    Task<IncomeCategoryResponse> CreateAsync(Guid academicYearId, CreateIncomeCategoryRequest input);
    Task<PaginatedIncomeCategoryResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null);
    Task<IncomeCategoryResponse> GetAsync(Guid id);
    Task<IncomeCategoryResponse> UpdateAsync(Guid id, UpdateIncomeCategoryRequest input);
    Task DeleteAsync(Guid id);
}

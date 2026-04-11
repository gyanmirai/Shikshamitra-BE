using System;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.Finance.Setting.ChargeCategory.Dtos;
using ShikshaMitra.Application.Contracts.Finance.Setting.FeeCategory.Dtos; // For MessageResponse if we reuse it, or create generic one.
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Application.Contracts.Finance.Setting.ChargeCategory;

public interface IChargeCategoryAppService : IApplicationService
{
    Task<ChargeCategoryResponse> CreateAsync(Guid academicYearId, CreateChargeCategoryRequest input);
    Task<PaginatedChargeCategoryResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null);
    Task<ChargeCategoryResponse> GetAsync(Guid id);
    Task<ChargeCategoryResponse> UpdateAsync(Guid id, UpdateChargeCategoryRequest input);
    Task DeleteAsync(Guid id);
}

using System;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.Finance.Setting.FeeCategory.Dtos; // For MessageResponse if we reuse it, or create generic one.
using ShikshaMitra.Finance.Fees.Settings.ChargeCategory.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.Settings.ChargeCategory;

public interface IChargeCategoryAppService : IApplicationService
{
    Task<ChargeCategoryResponse> CreateAsync(Guid academicYearId, CreateChargeCategoryRequest input);
    Task<PaginatedChargeCategoryResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null);
    Task<ChargeCategoryResponse> GetAsync(Guid id);
    Task<ChargeCategoryResponse> UpdateAsync(Guid id, UpdateChargeCategoryRequest input);
    Task DeleteAsync(Guid id);
}

using System;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.Settings.ChargeCategories;

public interface IChargeCategoryAppService : IApplicationService
{
    Task<ChargeCategoryResponse> CreateAsync(Guid academicYearId, CreateChargeCategoryRequest input);
    Task<PaginatedChargeCategoryResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null, bool? isActive = null, string? filters = null);
    Task<ChargeCategoryResponse> GetAsync(Guid id);
    Task<ChargeCategoryResponse> UpdateAsync(Guid id, UpdateChargeCategoryRequest input);
	Task<ChargeCategoryResponse> UpdateStatusAsync(Guid id, UpdateChargeCategoryStatusRequest input);
    Task DeleteAsync(Guid id);
}

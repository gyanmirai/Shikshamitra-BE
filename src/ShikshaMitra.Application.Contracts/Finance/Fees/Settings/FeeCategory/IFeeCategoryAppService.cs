using ShikshaMitra.Finance.Fees.Settings.Dtos;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.Settings;

public interface IFeeCategoryAppService : IApplicationService
{
    Task<FeeCategoryResponse> CreateAsync(Guid academicYearId, FeeCategoryCreateRequest input);
    Task<FeeCategoryListResponse> GetListAsync(Guid academicYearId, string? search = null, int page = 1, int pageSize = 10, bool? isActive = null, string? filters = null, string? sortBy = null, string? sortDirection = null);
    Task<FeeCategoryResponse> GetByIdAsync(Guid id);
    Task<FeeCategoryResponse> UpdateAsync(Guid id, FeeCategoryUpdateRequest input);
    Task<MessageResponse> DeleteAsync(Guid id);
    Task<FeeCategoryResponse> UpdateStatusAsync(Guid id, FeeCategoryStatusUpdateRequest input);
}

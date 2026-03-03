using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using ShikshaMitra.Application.Contracts.Finance.Setting.FeeCategory.Dtos;

namespace ShikshaMitra.Application.Contracts.Finance.Setting.FeeCategory;

public interface IFeeCategoryAppService : IApplicationService
{
    Task<MessageResponse> CreateAsync(Guid academicYearId, FeeCategoryCreateRequest input);
    Task<FeeCategoryListResponse> GetListAsync(Guid academicYearId, string search = null, int page = 1, int pageSize = 10);
    Task<MessageResponse> UpdateAsync(Guid feeCategoryId, FeeCategoryUpdateRequest input);
    Task<MessageResponse> DeleteAsync(Guid feeCategoryId);
}

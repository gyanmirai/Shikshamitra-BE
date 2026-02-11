using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using ShikshaMitra.Application.Contracts.Finance.Setting.FeeHead.Dtos;
using ShikshaMitra.Application.Contracts.Finance.Setting.FeeCategory.Dtos; // For MessageResponse

namespace ShikshaMitra.Application.Contracts.Finance.Setting.FeeHead;

public interface IFeeHeadAppService : IApplicationService
{
    Task<MessageResponse> CreateAsync(Guid academicYearId, FeeHeadCreateRequest input);
    Task<FeeHeadListResponse> GetListAsync(Guid academicYearId, Guid? feeCategoryId = null, string search = null, int page = 1, int pageSize = 10);
    Task<MessageResponse> UpdateAsync(Guid feeHeadId, FeeHeadUpdateRequest input);
    Task<MessageResponse> DeleteAsync(Guid feeHeadId);
    Task<MessageResponse> UpdateStatusAsync(Guid feeHeadId, FeeHeadStatusUpdateRequest input);
}

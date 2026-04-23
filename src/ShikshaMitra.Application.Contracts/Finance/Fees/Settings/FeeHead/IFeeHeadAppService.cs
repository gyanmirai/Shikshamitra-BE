using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using ShikshaMitra.Finance.Fees.Settings.FeeCategory.Dtos;
using ShikshaMitra.Finance.Fees.Settings.FeeHead.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings.FeeHead;

public interface IFeeHeadAppService : IApplicationService
{
    Task<MessageResponse> CreateAsync(Guid academicYearId, FeeHeadCreateRequest input);
    Task<FeeHeadListResponse> GetListAsync(Guid academicYearId, Guid? feeCategoryId = null, string search = null, int page = 1, int pageSize = 10);
    Task<MessageResponse> UpdateAsync(Guid feeHeadId, FeeHeadUpdateRequest input);
    Task<MessageResponse> DeleteAsync(Guid feeHeadId);
    Task<MessageResponse> UpdateStatusAsync(Guid feeHeadId, FeeHeadStatusUpdateRequest input);
}

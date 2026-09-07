using ShikshaMitra.Finance.Fees.Settings.Dtos;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.Settings;

public interface IFeeHeadAppService : IApplicationService
{
    Task<FeeHeadResponse> CreateAsync(Guid academicYearId, FeeHeadCreateRequest input);
    Task<FeeHeadListResponse> GetListAsync(Guid academicYearId, Guid? feeCategoryId = null, string? search = null, bool includeSuperseded = false, bool effectiveOnly = false, int page = 1, int pageSize = 10);
    Task<FeeHeadResponse> GetByIdAsync(Guid id);
    Task<FeeHeadResponse> UpdateAsync(Guid feeHeadId, FeeHeadUpdateRequest input);
    Task<MessageResponse> DeleteAsync(Guid feeHeadId);
    Task<FeeHeadResponse> UpdateStatusAsync(Guid feeHeadId, FeeHeadStatusUpdateRequest input);
    Task<FeeHeadImpactResponse> GetImpactAsync(Guid feeHeadId);
    Task<FeeHeadResponse> CreateRevisionAsync(Guid feeHeadId, FeeHeadRevisionRequest input);
}

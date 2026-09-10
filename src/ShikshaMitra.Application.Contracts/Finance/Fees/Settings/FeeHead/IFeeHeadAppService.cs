using ShikshaMitra.Finance.Fees.Settings.Dtos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.Settings;

public interface IFeeHeadAppService : IApplicationService
{
    Task<FeeHeadResponse> CreateAsync(Guid academicYearId, FeeHeadCreateRequest input);
    Task<FeeHeadListResponse> GetListAsync(Guid academicYearId, Guid? feeCategoryId = null, string search = null, int page = 1, int pageSize = 10);
	Task<List<ActiveFeeHeadLookupDto>> GetActiveAsync(Guid academicYearId);
    Task<FeeHeadResponse> GetByIdAsync(Guid id);
    Task<FeeHeadResponse> UpdateAsync(Guid feeHeadId, FeeHeadUpdateRequest input);
    Task<MessageResponse> DeleteAsync(Guid feeHeadId);
    Task<FeeHeadResponse> UpdateStatusAsync(Guid feeHeadId, FeeHeadStatusUpdateRequest input);
}

using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.FeeAdjustment;

public interface IFeeAdjustmentAppService : IApplicationService
{
    Task<AdjustmentEligibilityDto> GetEligibilityAsync(Guid studentId, Guid academicYearId);

    Task<FeeAdjustmentDetailDto> CreateAsync(Guid academicYearId, CreateFeeAdjustmentDto input);

    Task<FeeAdjustmentDetailDto> UpdateAsync(Guid id, Guid academicYearId, UpdateFeeAdjustmentDto input);

    Task DeleteAsync(Guid id);

    Task<FeeAdjustmentDetailDto> GetAsync(Guid id);

    Task<FeeAdjustmentApplicationsDto> GetApplicationsAsync(GetFeeAdjustmentApplicationsInput input);

    Task<FeeAdjustmentApplyContextDto> GetApplyContextAsync(Guid id);

    Task<ApplyFeeAdjustmentResultDto> ApplyAsync(Guid id, ApplyFeeAdjustmentDto input, string? idempotencyKey);

    Task<RejectFeeAdjustmentResultDto> RejectAsync(Guid id, RejectFeeAdjustmentDto input);
}

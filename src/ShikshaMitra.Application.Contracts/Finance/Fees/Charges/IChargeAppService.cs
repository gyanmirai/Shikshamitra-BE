using System;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Charges.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.Charges;

public interface IChargeAppService : IApplicationService
{
	Task<ChargeResponse> CreateAsync(Guid academicYearId, CreateChargeRequest input);

	Task<ChargeListResponse> GetAllAsync(Guid academicYearId, GetChargesRequest input);

	Task<ChargeDetailResponse> GetByIdAsync(Guid chargeId);

	Task<ChargeResponse> UpdateAsync(Guid chargeId, UpdateChargeRequest input);

	Task<ApprovalResponse> ApproveAsync(Guid chargeId, ApproveChargeRequest input);

	Task<RejectionResponse> RejectAsync(Guid chargeId, RejectChargeRequest input);

	Task DeleteAsync(Guid chargeId);
}

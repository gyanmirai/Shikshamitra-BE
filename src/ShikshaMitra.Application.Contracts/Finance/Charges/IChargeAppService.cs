using System;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.Finance.Charges.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Application.Contracts.Finance.Charges;

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

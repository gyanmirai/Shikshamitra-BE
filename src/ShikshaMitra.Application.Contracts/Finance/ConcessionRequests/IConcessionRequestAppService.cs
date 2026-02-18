using System;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.Finance.ConcessionRequests.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Application.Contracts.Finance.ConcessionRequests;

public interface IConcessionRequestAppService : IApplicationService
{
	// Step 1
	Task<CreateBasicDetailsResponse> CreateBasicDetailsAsync(CreateBasicDetailsRequest input);
	Task<GetBasicDetailsResponse> GetBasicDetailsAsync(Guid concessionRequestId);

	// Step 2
	Task<SaveConcessionTypesResponse> SaveConcessionTypesAsync(Guid concessionRequestId, SaveConcessionTypesRequest input);

	// Step 3
	Task<SaveFeeHeadsDataResponse> SaveFeeHeadsDataAsync(Guid concessionRequestId, SaveFeeHeadsDataRequest input);

	// Step 4
	Task<SaveValueEntryResponse> SaveValueEntryAsync(Guid concessionRequestId, SaveValueEntryRequest input);

	// Concession Requests list for a student
	Task<ConcessionListResponse> GetStudentConcessionRequestsAsync(Guid studentId, int? status, int page, int pageSize, string? search);

	// Approve / Reject
	Task<ApprovalResponse> ApproveConcessionRequestAsync(Guid studentConcessionTypeRequestId, ApprovalRequest input);
	Task<RejectionResponse> RejectConcessionRequestAsync(Guid studentConcessionTypeRequestId, RejectionRequest input);

	// Applications list (admin view)
	Task<ConcessionApplicationsResponse> GetConcessionApplicationsAsync(Guid boardId, Guid educationMediumId, int? status, int page, int pageSize, string? search);

	// Delete
	Task DeleteConcessionApplicationAsync(Guid studentConcessionTypeRequestId);
}

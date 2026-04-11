using System;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.Finance.ConcessionRequests.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Application.Contracts.Finance.ConcessionRequests;

public interface IConcessionRequestAppService : IApplicationService
{
	// Step 1+2 merged — Create concession request with concession types
	Task<CreateConcessionRequestResponse> CreateConcessionRequestAsync(CreateConcessionRequestInput input);
	Task<UpdateConcessionRequestResponse> UpdateConcessionRequestAsync(Guid concessionRequestId, UpdateConcessionRequestInput input);
	Task<GetBasicDetailsResponse> GetBasicDetailsAsync(Guid concessionRequestId);

	// Step 3
	Task<SaveFeeHeadsDataResponse> SaveFeeHeadsDataAsync(Guid concessionRequestId, SaveFeeHeadsDataRequest input);
	Task<GetFeeHeadsResponse> GetConcessionRequestFeeHeadsAsync(Guid concessionRequestId);

	// Step 4
	Task<SaveValueEntryResponse> SaveValueEntryAsync(Guid concessionRequestId, SaveValueEntryRequest input);
	Task<GetValueEntriesResponse> GetConcessionRequestValueEntriesAsync(Guid concessionRequestId);

	// Step 5 — Document Upload
	Task<UploadDocumentsResponse> UploadDocumentsAsync(Guid concessionRequestId, UploadDocumentsRequest input);
	Task<GetDocumentUploadResponse> GetConcessionRequestDocumentsForResumeAsync(Guid concessionRequestId);

	// Get documents list for a request
	Task<GetDocumentsResponse> GetConcessionDocumentsAsync(Guid studentConcessionTypeRequestId, int? status, int page, int pageSize);

	// Concession requests list for a student
	Task<ConcessionListResponse> GetStudentConcessionRequestsAsync(Guid studentId, int? status, int page, int pageSize, string? search);

	// Approve / Reject
	Task<ApprovalResponse> ApproveConcessionRequestAsync(Guid studentConcessionTypeRequestId, ApprovalRequest input);
	Task<RejectionResponse> RejectConcessionRequestAsync(Guid studentConcessionTypeRequestId, RejectionRequest input);

	// Applications list (admin view)
	Task<ConcessionApplicationsResponse> GetConcessionApplicationsAsync(Guid boardId, Guid educationMediumId, int? status, int page, int pageSize, string? search);

	// Delete
	Task DeleteConcessionApplicationAsync(Guid studentConcessionTypeRequestId);
}

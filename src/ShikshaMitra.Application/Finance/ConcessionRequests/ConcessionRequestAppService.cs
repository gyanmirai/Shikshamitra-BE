using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Application.Contracts.Finance.ConcessionRequests;
using ShikshaMitra.Application.Contracts.Finance.ConcessionRequests.Dtos;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Application.Finance.ConcessionRequests;

[Authorize]
[RemoteService(false)]
public class ConcessionRequestAppService : ApplicationService, IConcessionRequestAppService
{
	// ─── Step 1+2 POST (merged) ───────────────────────────────────────────────

	public async Task<CreateConcessionRequestResponse> CreateConcessionRequestAsync(CreateConcessionRequestInput input)
	{
		throw new NotImplementedException("Creation of concession request is now merged with update. Please use UpdateConcessionRequestAsync for both creating and updating concession requests.");
	}

	// ─── Step 1+2 PUT (Update) ────────────────────────────────────────────────

	public async Task<UpdateConcessionRequestResponse> UpdateConcessionRequestAsync(
		Guid concessionRequestId, UpdateConcessionRequestInput input)
	{
		throw new NotImplementedException("Creation of concession request is now merged with update. To create a new concession request, generate a new Guid and call this method with that Guid and the input data. For example: UpdateConcessionRequestAsync(Guid.NewGuid(), input).");
	}

	// ─── Step 1 GET ──────────────────────────────────────────────────────────

	public async Task<GetBasicDetailsResponse> GetBasicDetailsAsync(Guid concessionRequestId)
	{
		throw new NotImplementedException();
	}

	// ─── Step 3 PUT ──────────────────────────────────────────────────────────

	public async Task<SaveFeeHeadsDataResponse> SaveFeeHeadsDataAsync(Guid concessionRequestId, SaveFeeHeadsDataRequest input)
	{
		throw new NotImplementedException();
	}

	// ─── Step 4 PUT ──────────────────────────────────────────────────────────

	public async Task<SaveValueEntryResponse> SaveValueEntryAsync(Guid concessionRequestId, SaveValueEntryRequest input)
	{
		throw new NotImplementedException();
	}

	// ─── Step 5 PUT — Document Upload ────────────────────────────────────────

	public async Task<UploadDocumentsResponse> UploadDocumentsAsync(Guid concessionRequestId, UploadDocumentsRequest input)
	{
		throw new NotImplementedException();
	}

	// ─── GET Documents ────────────────────────────────────────────────────────

	public async Task<GetDocumentsResponse> GetConcessionDocumentsAsync(
		Guid studentConcessionTypeRequestId, int? status, int page, int pageSize)
	{
		throw new NotImplementedException();
	}

	// ─── Step 3 GET — Fee Heads (Edit/Resume) ────────────────────────────────

	public async Task<GetFeeHeadsResponse> GetConcessionRequestFeeHeadsAsync(Guid concessionRequestId)
	{
		throw new NotImplementedException();
	}

	// ─── Step 4 GET — Value Entries (Edit/Resume) ─────────────────────────────

	public async Task<GetValueEntriesResponse> GetConcessionRequestValueEntriesAsync(Guid concessionRequestId)
	{
		throw new NotImplementedException();
	}

	// ─── Step 5 GET — Document Upload (Edit/Resume) ───────────────────────────

	public async Task<GetDocumentUploadResponse> GetConcessionRequestDocumentsForResumeAsync(Guid concessionRequestId)
	{
		throw new NotImplementedException();
	}

	// ─── Get Student Concession Requests ─────────────────────────────────────

	public async Task<ConcessionListResponse> GetStudentConcessionRequestsAsync(
		Guid studentId, int? status, int page, int pageSize, string? search)
	{
		throw new NotImplementedException();
	}

	// ─── Approve ─────────────────────────────────────────────────────────────

	public async Task<ApprovalResponse> ApproveConcessionRequestAsync(
		Guid studentConcessionTypeRequestId, ApprovalRequest input)
	{
		throw new NotImplementedException();
	}

	// ─── Reject ──────────────────────────────────────────────────────────────

	public async Task<RejectionResponse> RejectConcessionRequestAsync(
		Guid studentConcessionTypeRequestId, RejectionRequest input)
	{
		throw new NotImplementedException();
	}

	// ─── Get Concession Applications (admin list) ─────────────────────────────

	public async Task<ConcessionApplicationsResponse> GetConcessionApplicationsAsync(
		Guid boardId, Guid educationMediumId, int? status, int page, int pageSize, string? search)
	{
		throw new NotImplementedException();
	}

	// ─── Delete ───────────────────────────────────────────────────────────────

	public async Task DeleteConcessionApplicationAsync(Guid studentConcessionTypeRequestId)
	{
		throw new NotImplementedException();
	}
}

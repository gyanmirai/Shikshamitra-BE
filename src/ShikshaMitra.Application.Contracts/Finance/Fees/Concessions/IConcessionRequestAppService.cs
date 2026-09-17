using System;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Common.Files;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.Concessions;

public interface IConcessionRequestAppService : IApplicationService
{
	Task<ApiDataResponseDto<ConcessionRequestDetailDto>> CreateAsync(Guid academicYearId, CreateConcessionRequestDto input);
	Task<ApiDataResponseDto<ConcessionRequestDetailDto>> CreateDraftAsync(Guid academicYearId, CreateConcessionDraftDto input);
	Task<ApiDataResponseDto<ConcessionRequestDetailDto>> SubmitAsync(Guid id, SubmitConcessionRequestDto input);
	Task<ConcessionRequestContextDto> GetContextAsync(Guid academicYearId, Guid studentId, Guid? studentFeeStructureId = null);
	Task<ConcessionRequestPreviewDto> PreviewAsync(Guid academicYearId, ConcessionRequestPreviewInputDto input);
	Task<ApiDataResponseDto<ConcessionRequestDetailDto>> UpdateAsync(Guid id, Guid academicYearId, UpdateConcessionRequestDto input);
	Task DeleteAsync(Guid id, DeleteConcessionRequestDto input);
	Task<ApiDataResponseDto<ConcessionRequestDetailDto>> GetAsync(Guid id);
	Task UploadDocumentsAsync(Guid id, UploadConcessionDocumentsDto input);
	Task<FileDownloadResult> DownloadDocumentAsync(Guid id, Guid documentId);
	Task<FileDownloadResult> DownloadStagedDocumentAsync(Guid uploadId);
	Task<ConcessionDocumentFileUploadResponseDto> UploadDocumentFileAsync(
		Guid concessionTypeId,
		Guid documentId,
		UploadConcessionDocumentFileDto input);
	Task<ConcessionApplicationsDto> GetApplicationsAsync(GetConcessionApplicationsInput input);
	Task<StudentConcessionRequestsDto> GetStudentRequestsAsync(Guid studentId, Guid academicYearId, PagedResultRequestDto input);
	Task<ConcessionApprovalContextDto> GetApprovalContextAsync(Guid id);
	Task<ConcessionApprovalPreviewDto> PreviewApprovalAsync(Guid id, PreviewConcessionApprovalDto input);
	Task<ConcessionApprovalResponseDto> ApproveAsync(Guid id, ApproveConcessionRequestDto input);
	Task<ConcessionRejectionResponseDto> RejectAsync(Guid id, RejectConcessionRequestDto input);
	Task<ConcessionCancellationResponseDto> CancelAsync(Guid id, CancelConcessionRequestDto input);
	Task<ConcessionReversalResponseDto> ReverseAsync(Guid id, ReverseConcessionRequestDto input);
	Task<ConcessionReversalPreviewDto> PreviewReversalAsync(Guid id, PreviewConcessionReversalDto input);
}

public class GetConcessionApplicationsInput : PagedResultRequestDto, IFinanceClassDivisionFilter
{
	public Guid AcademicYearId { get; set; }
	public Guid BoardId { get; set; }
	public Guid EducationMediumId { get; set; }
	public int? Status { get; set; }
	public Guid? StandardId { get; set; }
	public Guid? DivisionId { get; set; }
}

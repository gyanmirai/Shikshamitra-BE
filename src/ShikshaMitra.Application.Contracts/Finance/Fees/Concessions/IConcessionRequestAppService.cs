using System;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.Concessions;

public interface IConcessionRequestAppService : IApplicationService
{
	Task<ApiDataResponseDto<ConcessionRequestDetailDto>> CreateAsync(Guid academicYearId, CreateConcessionRequestDto input);
	Task<ApiDataResponseDto<ConcessionRequestDetailDto>> UpdateAsync(Guid id, Guid academicYearId, UpdateConcessionRequestDto input);
	Task DeleteAsync(Guid id);
	Task<ApiDataResponseDto<ConcessionRequestDetailDto>> GetAsync(Guid id);
	Task UploadDocumentsAsync(Guid id, UploadConcessionDocumentsDto input);
	Task<ConcessionDocumentFileUploadResponseDto> UploadDocumentFileAsync(
		Guid concessionTypeId,
		Guid documentId,
		UploadConcessionDocumentFileDto input);
	Task<ConcessionApplicationsDto> GetApplicationsAsync(GetConcessionApplicationsInput input);
	Task<StudentConcessionRequestsDto> GetStudentRequestsAsync(Guid studentId, PagedResultRequestDto input);
	Task<ConcessionApprovalContextDto> GetApprovalContextAsync(Guid id);
	Task<ConcessionApprovalResponseDto> ApproveAsync(Guid id, ApproveConcessionRequestDto input);
	Task<ConcessionRejectionResponseDto> RejectAsync(Guid id, RejectConcessionRequestDto input);
	Task<ConcessionCancellationResponseDto> CancelAsync(Guid id);
}

public class GetConcessionApplicationsInput : PagedResultRequestDto, IFinanceClassDivisionFilter
{
	public Guid BoardId { get; set; }
	public Guid EducationMediumId { get; set; }
	public int? Status { get; set; }
	public Guid? StandardId { get; set; }
	public Guid? DivisionId { get; set; }
}

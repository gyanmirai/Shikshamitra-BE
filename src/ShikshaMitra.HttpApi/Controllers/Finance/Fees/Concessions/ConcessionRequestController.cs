using System;
using System.IO;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Models;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Fees.Concessions;

[RemoteService]
[Area("finance")]
[Route("api/concession-request")]
[Authorize]
public class ConcessionRequestController(IConcessionRequestAppService concessionRequestAppService)
	: AbpController
{
	[HttpPost]
	[ProducesResponseType(typeof(ApiDataResponseDto<ConcessionRequestDetailDto>), StatusCodes.Status200OK)]
	public Task<ApiDataResponseDto<ConcessionRequestDetailDto>> CreateAsync(
		[FromQuery] Guid academicYearId,
		[FromBody] CreateConcessionRequestDto input) =>
		concessionRequestAppService.CreateAsync(academicYearId, input);

	[HttpPost("drafts")]
	[ProducesResponseType(typeof(ApiDataResponseDto<ConcessionRequestDetailDto>), StatusCodes.Status200OK)]
	public Task<ApiDataResponseDto<ConcessionRequestDetailDto>> CreateDraftAsync(
		[FromQuery] Guid academicYearId,
		[FromBody] CreateConcessionDraftDto input) =>
		concessionRequestAppService.CreateDraftAsync(academicYearId, input);

	[HttpPost("{id:guid}/submit")]
	[ProducesResponseType(typeof(ApiDataResponseDto<ConcessionRequestDetailDto>), StatusCodes.Status200OK)]
	public Task<ApiDataResponseDto<ConcessionRequestDetailDto>> SubmitAsync(
		Guid id,
		[FromBody] SubmitConcessionRequestDto input) =>
		concessionRequestAppService.SubmitAsync(id, input);

	[HttpGet("context")]
	[ProducesResponseType(typeof(ConcessionRequestContextDto), StatusCodes.Status200OK)]
	public Task<ConcessionRequestContextDto> GetContextAsync(
		[FromQuery] Guid academicYearId,
		[FromQuery] Guid studentId,
		[FromQuery] Guid? studentFeeStructureId = null,
		[FromQuery] Guid? concessionTypeId = null,
		[FromQuery] Guid? concessionPolicyId = null,
		[FromQuery] Guid? concessionPolicyVersionId = null) =>
		concessionRequestAppService.GetContextAsync(academicYearId, studentId, studentFeeStructureId,
			concessionTypeId, concessionPolicyId, concessionPolicyVersionId);

	[HttpPost("preview")]
	[ProducesResponseType(typeof(ConcessionRequestPreviewDto), StatusCodes.Status200OK)]
	public Task<ConcessionRequestPreviewDto> PreviewAsync(
		[FromQuery] Guid academicYearId,
		[FromBody] ConcessionRequestPreviewInputDto input) =>
		concessionRequestAppService.PreviewAsync(academicYearId, input);

	[HttpPut("{id:guid}")]
	[ProducesResponseType(typeof(ApiDataResponseDto<ConcessionRequestDetailDto>), StatusCodes.Status200OK)]
	public Task<ApiDataResponseDto<ConcessionRequestDetailDto>> UpdateAsync(
		Guid id,
		[FromQuery] Guid academicYearId,
		[FromBody] UpdateConcessionRequestDto input) =>
		concessionRequestAppService.UpdateAsync(id, academicYearId, input);

	[HttpDelete("{id:guid}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> DeleteAsync(Guid id, [FromBody] DeleteConcessionRequestDto input)
	{
		await concessionRequestAppService.DeleteAsync(id, input);
		return NoContent();
	}

	[HttpGet("{id:guid}")]
	[ProducesResponseType(typeof(ApiDataResponseDto<ConcessionRequestDetailDto>), StatusCodes.Status200OK)]
	public Task<ApiDataResponseDto<ConcessionRequestDetailDto>> GetAsync(Guid id) =>
		concessionRequestAppService.GetAsync(id);

	[HttpPut("{id:guid}/documents")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> UploadDocumentsAsync(Guid id, [FromBody] UploadConcessionDocumentsDto input)
	{
		await concessionRequestAppService.UploadDocumentsAsync(id, input);
		return NoContent();
	}

	[HttpGet("{id:guid}/documents/{documentId:guid}/file")]
	[ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
	public async Task<IActionResult> DownloadDocumentAsync(Guid id, Guid documentId)
	{
		var file = await concessionRequestAppService.DownloadDocumentAsync(id, documentId);
		return File(file.Content, file.ContentType, file.FileName);
	}

	[HttpGet("staged-documents/{uploadId:guid}/file")]
	[ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
	public async Task<IActionResult> DownloadStagedDocumentAsync(Guid uploadId)
	{
		var file = await concessionRequestAppService.DownloadStagedDocumentAsync(uploadId);
		return File(file.Content, file.ContentType, file.FileName);
	}

	[HttpPost("types/{concessionTypeId:guid}/documents/{documentId:guid}/file")]
	[Consumes("multipart/form-data")]
	// Leave room for multipart headers; the application service still enforces the 20 MB file limit.
	[RequestFormLimits(MultipartBodyLengthLimit = 21L * 1024 * 1024)]
	[ProducesResponseType(typeof(ConcessionDocumentFileUploadResponseDto), StatusCodes.Status200OK)]
	public async Task<ConcessionDocumentFileUploadResponseDto> UploadDocumentFileAsync(
		Guid concessionTypeId,
		Guid documentId,
		[FromForm] ConcessionDocumentUploadRequest input)
	{
		using var stream = new MemoryStream();
		await input.File.CopyToAsync(stream);
		return await concessionRequestAppService.UploadDocumentFileAsync(
			concessionTypeId,
			documentId,
			new UploadConcessionDocumentFileDto
			{
				FileName = input.File.FileName,
				ContentType = input.File.ContentType,
				Content = stream.ToArray()
			});
	}

	[HttpGet("concession-applications")]
	[ProducesResponseType(typeof(ConcessionApplicationsDto), StatusCodes.Status200OK)]
	public Task<ConcessionApplicationsDto> GetApplicationsAsync(
		[FromQuery] Guid academicYearId,
		[FromQuery] Guid boardId,
		[FromQuery] Guid educationMediumId,
		[FromQuery] Guid? standardId = null,
		[FromQuery] Guid? divisionId = null,
		[FromQuery, Range(1, int.MaxValue)] int page = 1,
		[FromQuery, Range(1, 100)] int pageSize = 20,
		[FromQuery] int? status = null) =>
		concessionRequestAppService.GetApplicationsAsync(new GetConcessionApplicationsInput
		{
			AcademicYearId = academicYearId,
			BoardId = boardId,
			EducationMediumId = educationMediumId,
			StandardId = standardId,
			DivisionId = divisionId,
			SkipCount = (page - 1) * pageSize,
			MaxResultCount = pageSize,
			Status = status,
		});

	[HttpGet("{id:guid}/approval-context")]
	[ProducesResponseType(typeof(ConcessionApprovalContextDto), StatusCodes.Status200OK)]
	public Task<ConcessionApprovalContextDto> GetApprovalContextAsync(Guid id) =>
		concessionRequestAppService.GetApprovalContextAsync(id);
}

[RemoteService]
[Area("finance")]
[Route("api/concession-requests")]
[Authorize]
public class ConcessionRequestsController(IConcessionRequestAppService concessionRequestAppService)
	: AbpController
{
	[HttpGet("{studentId:guid}")]
	[ProducesResponseType(typeof(StudentConcessionRequestsDto), StatusCodes.Status200OK)]
	public Task<StudentConcessionRequestsDto> GetStudentRequestsAsync(
		Guid studentId,
		[FromQuery] Guid academicYearId,
		[FromQuery, Range(1, int.MaxValue)] int page = 1,
		[FromQuery, Range(1, 100)] int pageSize = 10) =>
		concessionRequestAppService.GetStudentRequestsAsync(studentId, academicYearId, new PagedResultRequestDto
		{
			SkipCount = (page - 1) * pageSize,
			MaxResultCount = pageSize,
		});

	[HttpPost("{id:guid}/approve")]
	[ProducesResponseType(typeof(ConcessionApprovalResponseDto), StatusCodes.Status200OK)]
	public Task<ConcessionApprovalResponseDto> ApproveAsync(Guid id, [FromBody] ApproveConcessionRequestDto input) =>
		concessionRequestAppService.ApproveAsync(id, input);

	[HttpPost("{id:guid}/approval-preview")]
	[ProducesResponseType(typeof(ConcessionApprovalPreviewDto), StatusCodes.Status200OK)]
	public Task<ConcessionApprovalPreviewDto> PreviewApprovalAsync(
		Guid id, [FromBody] PreviewConcessionApprovalDto input) =>
		concessionRequestAppService.PreviewApprovalAsync(id, input);

	[HttpPost("{id:guid}/reject")]
	[ProducesResponseType(typeof(ConcessionRejectionResponseDto), StatusCodes.Status200OK)]
	public Task<ConcessionRejectionResponseDto> RejectAsync(Guid id, [FromBody] RejectConcessionRequestDto input) =>
		concessionRequestAppService.RejectAsync(id, input);

	[HttpPost("{id:guid}/cancel")]
	[ProducesResponseType(typeof(ConcessionCancellationResponseDto), StatusCodes.Status200OK)]
	public Task<ConcessionCancellationResponseDto> CancelAsync(
		Guid id, [FromBody] CancelConcessionRequestDto input) =>
		concessionRequestAppService.CancelAsync(id, input);

	[HttpPost("{id:guid}/reverse")]
	[ProducesResponseType(typeof(ConcessionReversalResponseDto), StatusCodes.Status200OK)]
	public Task<ConcessionReversalResponseDto> ReverseAsync(
		Guid id, [FromBody] ReverseConcessionRequestDto input) =>
		concessionRequestAppService.ReverseAsync(id, input);

	[HttpPost("{id:guid}/reversal-preview")]
	[ProducesResponseType(typeof(ConcessionReversalPreviewDto), StatusCodes.Status200OK)]
	public Task<ConcessionReversalPreviewDto> PreviewReversalAsync(
		Guid id, [FromBody] PreviewConcessionReversalDto input) =>
		concessionRequestAppService.PreviewReversalAsync(id, input);
}

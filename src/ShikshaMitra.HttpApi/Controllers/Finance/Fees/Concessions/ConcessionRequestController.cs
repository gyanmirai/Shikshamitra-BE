using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Fees.Concessions;

[RemoteService]
[Area("app")]
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

	[HttpPut("{id:guid}")]
	[ProducesResponseType(typeof(ApiDataResponseDto<ConcessionRequestDetailDto>), StatusCodes.Status200OK)]
	public Task<ApiDataResponseDto<ConcessionRequestDetailDto>> UpdateAsync(
		Guid id,
		[FromQuery] Guid academicYearId,
		[FromBody] UpdateConcessionRequestDto input) =>
		concessionRequestAppService.UpdateAsync(id, academicYearId, input);

	[HttpDelete("{id:guid}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> DeleteAsync(Guid id)
	{
		await concessionRequestAppService.DeleteAsync(id);
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

	[HttpGet("concession-applications")]
	[ProducesResponseType(typeof(ConcessionApplicationsDto), StatusCodes.Status200OK)]
	public Task<ConcessionApplicationsDto> GetApplicationsAsync(
		[FromQuery] Guid boardId,
		[FromQuery] Guid educationMediumId,
		[FromQuery] Guid? standardId = null,
		[FromQuery] Guid? divisionId = null,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20,
		[FromQuery] int? status = null) =>
		concessionRequestAppService.GetApplicationsAsync(new GetConcessionApplicationsInput
		{
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

	[HttpPost("{id:guid}/approve")]
	[ProducesResponseType(typeof(ConcessionApprovalResponseDto), StatusCodes.Status200OK)]
	public Task<ConcessionApprovalResponseDto> ApproveAsync(Guid id, [FromBody] ApproveConcessionRequestDto input) =>
		concessionRequestAppService.ApproveAsync(id, input);

	[HttpPost("{id:guid}/reject")]
	[ProducesResponseType(typeof(ConcessionRejectionResponseDto), StatusCodes.Status200OK)]
	public Task<ConcessionRejectionResponseDto> RejectAsync(Guid id, [FromBody] RejectConcessionRequestDto input) =>
		concessionRequestAppService.RejectAsync(id, input);
}

[RemoteService]
[Area("app")]
[Route("api/concession-requests")]
[Authorize]
public class ConcessionRequestsController(IConcessionRequestAppService concessionRequestAppService)
	: AbpController
{
	[HttpGet("{studentId:guid}")]
	[ProducesResponseType(typeof(StudentConcessionRequestsDto), StatusCodes.Status200OK)]
	public Task<StudentConcessionRequestsDto> GetStudentRequestsAsync(
		Guid studentId,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 10) =>
		concessionRequestAppService.GetStudentRequestsAsync(studentId, new PagedResultRequestDto
		{
			SkipCount = (page - 1) * pageSize,
			MaxResultCount = pageSize,
		});

	[HttpPost("{id:guid}/approve")]
	[ProducesResponseType(typeof(ConcessionApprovalResponseDto), StatusCodes.Status200OK)]
	public Task<ConcessionApprovalResponseDto> ApproveAsync(Guid id, [FromBody] ApproveConcessionRequestDto input) =>
		concessionRequestAppService.ApproveAsync(id, input);

	[HttpPost("{id:guid}/reject")]
	[ProducesResponseType(typeof(ConcessionRejectionResponseDto), StatusCodes.Status200OK)]
	public Task<ConcessionRejectionResponseDto> RejectAsync(Guid id, [FromBody] RejectConcessionRequestDto input) =>
		concessionRequestAppService.RejectAsync(id, input);

	[HttpPost("{id:guid}/cancel")]
	[ProducesResponseType(typeof(ConcessionCancellationResponseDto), StatusCodes.Status200OK)]
	public Task<ConcessionCancellationResponseDto> CancelAsync(Guid id) =>
		concessionRequestAppService.CancelAsync(id);
}

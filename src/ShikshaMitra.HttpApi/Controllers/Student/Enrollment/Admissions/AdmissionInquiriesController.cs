using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[ApiController]
[Authorize]
[Route("api/student/enrollment/admissions/inquiries")]
public class AdmissionInquiriesController(IAdmissionInquiryAppService service) : AbpController
{
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.View)]
	public async Task<Result<PagedResultDto<AdmissionInquiryDto>>> GetListAsync([FromQuery] AdmissionInquiryListInput input) => Result.Success(await service.GetListAsync(input));

	[HttpGet("export")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.View)]
	public async Task<IActionResult> ExportCsvAsync([FromQuery] AdmissionInquiryListInput input) =>
		File(await service.ExportCsvAsync(input), "text/csv; charset=utf-8",
			$"admission-inquiries-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");

	[HttpGet("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.View)]
	public async Task<Result<AdmissionInquiryDto>> GetAsync(Guid id) => Result.Success(await service.GetAsync(id));

	[HttpGet("{id:guid}/timeline")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.View)]
	public async Task<Result<List<AdmissionInquiryTimelineItemDto>>> GetTimelineAsync(Guid id) =>
		Result.Success(await service.GetTimelineAsync(id));

	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<Result<AdmissionInquiryDto>> CreateAsync(CreateAdmissionInquiryDto input) => Result.Success(await service.CreateAsync(input));

	[HttpPut("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<Result<AdmissionInquiryDto>> UpdateAsync(Guid id, UpdateAdmissionInquiryDto input) =>
		Result.Success(await service.UpdateAsync(id, input));

	[HttpPost("{id:guid}/status")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<Result<AdmissionInquiryDto>> SetStatusAsync(Guid id, [FromQuery] AdmissionInquiryStatus status, [FromQuery] Guid? duplicateOfId = null) => Result.Success(await service.SetStatusAsync(id, status, duplicateOfId));

	[HttpPost("{id:guid}/contacted")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<Result<AdmissionInquiryDto>> MarkContactedAsync(Guid id) => Result.Success(await service.MarkContactedAsync(id));

	[HttpPost("duplicate-check")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<Result<System.Collections.Generic.List<AdmissionInquiryDuplicateMatchDto>>> DuplicateCheckAsync(AdmissionInquiryDuplicateCheckInput input) => Result.Success(await service.DuplicateCheckAsync(input));

	[HttpGet("counsellors")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Assign)]
	public async Task<Result<List<AdmissionInquiryCounsellorDto>>> GetAssignableCounsellorsAsync(
		[FromQuery] Guid? admissionOfferingId = null,
		[FromQuery] string? filter = null) =>
		Result.Success(await service.GetAssignableCounsellorsAsync(admissionOfferingId, filter));

	[HttpPost("{id:guid}/assign")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Assign)]
	public async Task<Result<AdmissionInquiryDto>> AssignCounsellorAsync(Guid id, AssignAdmissionCounsellorDto input) => Result.Success(await service.AssignCounsellorAsync(id, input));

	[HttpPost("bulk/assign")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Assign)]
	public async Task<Result<BulkAdmissionInquiryResultDto>> BulkAssignCounsellorAsync(BulkAssignAdmissionInquiriesDto input) =>
		Result.Success(await service.BulkAssignCounsellorAsync(input));

	[HttpPost("{id:guid}/communications")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<Result<AdmissionInquiryCommunicationResultDto>> CreateCommunicationAsync(
		Guid id,
		CreateAdmissionInquiryCommunicationDto input) =>
		Result.Success(await service.CreateCommunicationAsync(id, input));
}

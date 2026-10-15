using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Student.Enrollment.Admissions;

[ApiController, Authorize, Route("api/student/enrollment/admissions/public-inquiry-links")]
public class AdmissionPublicInquiryLinksController(IAdmissionPublicInquiryAppService service) : AbpController
{
	[HttpGet, Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public async Task<Result<List<AdmissionPublicInquiryLinkDto>>> GetListAsync() =>
		Result.Success(await service.GetLinksAsync());

	[HttpGet("options"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public async Task<Result<List<AdmissionPublicInquiryLinkOptionDto>>> GetOptionsAsync([FromQuery] Guid admissionCycleId) =>
		Result.Success(await service.GetLinkOptionsAsync(admissionCycleId));

	[HttpPost, Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add)]
	public async Task<Result<AdmissionPublicInquiryLinkCreatedDto>> CreateAsync(
		[FromBody] CreateAdmissionPublicInquiryLinkDto input) =>
		Result.Success(await service.CreateLinkAsync(input));

	[HttpPut("{id:guid}/form"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add)]
	public async Task<Result<AdmissionPublicInquiryLinkDto>> SetFormAsync(Guid id,
		[FromBody] SetAdmissionPublicInquiryLinkFormDto input) =>
		Result.Success(await service.SetLinkFormAsync(id, input));

	[HttpPost("{id:guid}/revoke"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add)]
	public async Task<IActionResult> RevokeAsync(Guid id, [FromBody] RevokeAdmissionPublicInquiryLinkDto input)
	{
		await service.RevokeLinkAsync(id, input.ConcurrencyStamp);
		return NoContent();
	}
}

public class RevokeAdmissionPublicInquiryLinkDto
{
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

[ApiController, AllowAnonymous, Route("api/public/admission-inquiries")]
public class PublicAdmissionInquiriesController(IAdmissionPublicInquiryAppService service) : AbpController
{
	[HttpGet("{token}/branding/{kind:int}")]
	public async Task<IActionResult> BrandingAsync(string token, ShikshaMitra.TenantSettings.SchoolIdentity.SchoolIdentityAssetKind kind,
		[FromQuery] Guid? offeringId = null)
	{
		var file = await service.GetPublicBrandingAssetAsync(token, kind, offeringId);
		Response.Headers.CacheControl = "no-store";
		Response.Headers["X-Content-Type-Options"] = "nosniff";
		return File(file.Content, file.ContentType);
	}
	[HttpGet("{token}")]
	public Task<PublicAdmissionInquiryContextDto> GetAsync(string token, [FromQuery] Guid? offeringId = null) =>
		service.GetPublicAsync(token, offeringId);

	[HttpPost("{token}")]
	[EnableRateLimiting("public-admission-inquiry")]
	[ProducesResponseType(StatusCodes.Status202Accepted)]
	public async Task<IActionResult> SubmitAsync(string token, [FromBody] SubmitPublicAdmissionInquiryDto input)
	{
		await service.SubmitPublicAsync(token, input);
		return Accepted();
	}
}

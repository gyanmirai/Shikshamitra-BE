using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[ApiController, Route("api/student/enrollment/admissions/inquiries"),
 Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Create)]
public class AdmissionInquiryCaptureController(IAdmissionInquiryCaptureAppService service) : AbpController
{
	[HttpGet("capture-context/{offeringId:guid}")]
	public async Task<Result<AdmissionInquiryCaptureContextDto>> GetContextAsync(Guid offeringId) =>
		Result.Success(await service.GetContextAsync(offeringId));

	[HttpGet("capture-catalogue")]
	public async Task<Result<AdmissionInquiryCaptureCatalogueDto>> GetCatalogueAsync([FromQuery] Guid? academicYearId = null) =>
		Result.Success(await service.GetCatalogueAsync(academicYearId));
}

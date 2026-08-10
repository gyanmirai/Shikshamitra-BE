using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.AdmissionsCrm;

[ApiController]
[Authorize]
[Route("api/student-enrollment/admissions-crm")]
public class AdmissionCrmWorkspaceController(IAdmissionCrmWorkspaceAppService appService) : AbpControllerBase
{
	[HttpGet("workspace/applicants/{leadId:guid}/progress")]
	public Task<AdmissionCrmApplicantProgressDto> GetApplicantProgressAsync(Guid leadId) =>
		appService.GetApplicantProgressAsync(leadId);

	[HttpGet("workspace/staff")]
	public Task<IReadOnlyList<AdmissionCrmStaffLookupDto>> GetStaffAsync(
		[FromQuery] string? filter = null,
		[FromQuery] string? ids = null,
		[FromQuery] int maxResultCount = 20,
		[FromQuery] Guid? academicYearId = null,
		[FromQuery] bool ownerOnly = false,
		[FromQuery] bool reviewerOnly = false) =>
		appService.GetStaffAsync(filter, ids, maxResultCount, academicYearId, ownerOnly, reviewerOnly);

	[HttpGet("{academicYearId}/workspace/integration-capabilities")]
	public Task<IReadOnlyList<AdmissionCrmIntegrationCapabilityDto>> GetIntegrationCapabilitiesAsync(Guid academicYearId) =>
		appService.GetIntegrationCapabilitiesAsync(academicYearId);
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.AdmissionsCrm;

[ApiController]
[Authorize]
[Route("api/student-enrollment/admissions-crm")]
public class AdmissionCrmAnalyticsController(
	IAdmissionCrmAnalyticsAppService admissionCrmAnalyticsAppService) : AbpControllerBase
{
	[HttpGet("{academicYearId}/analytics/summary")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Analytics)]
	public async Task<AdmissionCrmAnalyticsSummaryDto> GetSummaryAsync(
		[FromRoute] Guid academicYearId,
		[FromQuery] GetAdmissionCrmAnalyticsSummaryInput input)
	{
		return await admissionCrmAnalyticsAppService.GetSummaryAsync(academicYearId, input);
	}
}

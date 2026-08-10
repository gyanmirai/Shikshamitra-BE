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
	IAdmissionCrmAnalyticsSummaryService analyticsSummaryService,
	IAdmissionCrmReportAppService admissionCrmReportAppService) : AbpControllerBase
{
	[HttpGet("{academicYearId}/analytics/summary")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Analytics)]
	public async Task<AdmissionCrmAnalyticsSummaryDto> GetSummaryAsync(
		[FromRoute] Guid academicYearId,
		[FromQuery] GetAdmissionCrmAnalyticsSummaryInput input)
	{
		return await analyticsSummaryService.GetAsync(academicYearId, input);
	}

	[HttpGet("{academicYearId}/reports/admissions")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Analytics)]
	public Task<AdmissionCrmAdmissionReportDto> GetAdmissionReportAsync(
		[FromRoute] Guid academicYearId,
		[FromQuery] GetAdmissionCrmAdmissionReportInput input)
	{
		return admissionCrmReportAppService.GetAdmissionReportAsync(academicYearId, input);
	}
}

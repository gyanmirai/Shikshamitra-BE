using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.AdmissionsCrm;

[ApiController]
[Authorize]
[Route("api/student-enrollment/admissions-crm")]
public class AdmissionCrmStructuredReviewController(IAdmissionCrmStructuredReviewAppService service) : AbpControllerBase
{
	[HttpGet("{academicYearId}/structured-review/configuration")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewStructuredReviews)]
	public Task<AdmissionCrmReviewConfigurationDto> GetConfigurationAsync([FromRoute] Guid academicYearId) =>
		service.GetConfigurationAsync(academicYearId);

	[HttpPut("{academicYearId}/structured-review/configuration")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageReviewConfiguration)]
	public Task<AdmissionCrmReviewConfigurationDto> UpdateConfigurationAsync([FromRoute] Guid academicYearId,
		[FromBody] UpdateAdmissionCrmReviewConfigurationDto input) => service.UpdateConfigurationAsync(academicYearId, input);

	[HttpGet("leads/{leadId}/structured-review")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewStructuredReviews)]
	public Task<AdmissionCrmStructuredReviewDto?> GetByLeadAsync([FromRoute] Guid leadId) => service.GetByLeadAsync(leadId);

	[HttpGet("{academicYearId}/structured-review/my-assignments")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.SubmitReviews)]
	public Task<IReadOnlyList<AdmissionCrmReviewAssignmentDto>> GetMyAssignmentsAsync([FromRoute] Guid academicYearId) =>
		service.GetMyAssignmentsAsync(academicYearId);

	[HttpPost("leads/{leadId}/structured-review/start")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.AssignReviewers)]
	public Task<AdmissionCrmStructuredReviewDto> StartAsync([FromRoute] Guid leadId) => service.StartAsync(leadId);

	[HttpPost("structured-reviews/{reviewId}/assignments")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.AssignReviewers)]
	public Task<AdmissionCrmStructuredReviewDto> AssignAsync([FromRoute] Guid reviewId,
		[FromBody] AssignAdmissionCrmReviewerDto input) => service.AssignAsync(reviewId, input);

	[HttpPost("structured-review-assignments/{assignmentId}/conflict")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.SubmitReviews)]
	public Task<AdmissionCrmReviewAssignmentDto> DeclareConflictAsync([FromRoute] Guid assignmentId,
		[FromBody] DeclareAdmissionCrmReviewConflictDto input) => service.DeclareConflictAsync(assignmentId, input);

	[HttpPost("structured-review-assignments/{assignmentId}/submit")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.SubmitReviews)]
	public Task<AdmissionCrmReviewAssignmentDto> SubmitAsync([FromRoute] Guid assignmentId,
		[FromBody] SubmitAdmissionCrmStructuredReviewDto input) => service.SubmitAsync(assignmentId, input);

	[HttpPost("structured-reviews/{reviewId}/close")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.MakeAdmissionDecision)]
	public Task<AdmissionCrmStructuredReviewDto> CloseAsync([FromRoute] Guid reviewId,
		[FromBody] CloseAdmissionCrmStructuredReviewDto input) => service.CloseAsync(reviewId, input);
}

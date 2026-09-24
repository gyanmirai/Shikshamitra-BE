using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[ApiController]
[Authorize]
[Route("api/student/enrollment/admissions/applications")]
public class AdmissionApplicationsController(IAdmissionApplicationAppService service) : AbpController
{
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public async Task<Result<Volo.Abp.Application.Dtos.PagedResultDto<AdmissionApplicationDto>>> GetListAsync([FromQuery] AdmissionApplicationListInput input) => Result.Success(await service.GetListAsync(input));

	[HttpGet("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public async Task<ActionResult<Result<AdmissionApplicationDto>>> GetAsync(Guid id)
	{
		var application = await service.FindAsync(id);
		return application == null ? NotFound() : Result.Success(application);
	}

	[HttpPost("start")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Start)]
	public async Task<Result<AdmissionApplicationDto>> StartAsync([FromQuery] Guid applicantId, [FromQuery] Guid offeringId, [FromQuery] Guid? inquiryId = null) => Result.Success(await service.StartAsync(applicantId, offeringId, inquiryId));

	[HttpPost("/api/student/enrollment/admissions/applicants/{applicantId:guid}/applications/start")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Start)]
	public async Task<Result<AdmissionApplicationDto>> StartForApplicantAsync(Guid applicantId, [FromQuery] Guid offeringId, [FromQuery] Guid? inquiryId = null) => Result.Success(await service.StartAsync(applicantId, offeringId, inquiryId));

	[HttpGet("{id:guid}/workflow")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public async Task<Result<AdmissionApplicationWorkflowDto>> GetWorkflowAsync(Guid id) => Result.Success(await service.GetWorkflowAsync(id));

	[HttpGet("{id:guid}/form")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public async Task<Result<AdmissionApplicationFormDto>> GetFormAsync(Guid id) => Result.Success(await service.GetFormAsync(id));

	[HttpPatch("{id:guid}/form-values")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Edit)]
	public async Task<Result<AdmissionApplicationFormDto>> UpdateFormValuesAsync(Guid id, UpdateAdmissionApplicationFormValuesDto input) => Result.Success(await service.UpdateFormValuesAsync(id, input));

	[HttpPost("{id:guid}/submit")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Submit)]
	public async Task<Result<AdmissionApplicationSubmitResultDto>> SubmitAsync(Guid id) => Result.Success(await service.SubmitAsync(id));

	[HttpPost("{id:guid}/form/reopen")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Reopen)]
	public async Task<Result<AdmissionApplicationFormDto>> ReopenFormAsync(Guid id) => Result.Success(await service.ReopenFormAsync(id));

	[HttpGet("{id:guid}/form/history")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public async Task<Result<System.Collections.Generic.List<AdmissionFormChangeHistoryDto>>> GetFormHistoryAsync(Guid id) => Result.Success(await service.GetFormHistoryAsync(id));

	[HttpPost("{id:guid}/workflow/advance")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Advance)]
	public async Task<Result<AdmissionApplicationWorkflowDto>> AdvanceWorkflowAsync(Guid id, AdvanceAdmissionWorkflowDto input) =>
		Result.Success(await service.AdvanceWorkflowAsync(id, input));

	[HttpGet("{id:guid}/decision-history")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public async Task<Result<System.Collections.Generic.List<AdmissionApplicationDecisionHistoryDto>>> GetDecisionHistoryAsync(Guid id) => Result.Success(await service.GetDecisionHistoryAsync(id));

	[HttpPost("{id:guid}/decision/review")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Decide)]
	public async Task<Result<AdmissionApplicationDto>> MarkInReviewAsync(Guid id, AdmissionApplicationDecisionDto input) => Result.Success(await service.MarkInReviewAsync(id, input));

	[HttpPost("{id:guid}/decision/approve")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Decide)]
	public async Task<Result<AdmissionApplicationDto>> ApproveAsync(Guid id, AdmissionApplicationDecisionDto input) => Result.Success(await service.ApproveAsync(id, input));

	[HttpPost("{id:guid}/decision/reject")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Decide)]
	public async Task<Result<AdmissionApplicationDto>> RejectAsync(Guid id, AdmissionApplicationDecisionDto input) => Result.Success(await service.RejectAsync(id, input));

	[HttpPost("{id:guid}/decision/waitlist")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Decide)]
	public async Task<Result<AdmissionApplicationDto>> WaitlistAsync(Guid id, AdmissionApplicationDecisionDto input) => Result.Success(await service.WaitlistAsync(id, input));

	[HttpPost("{id:guid}/offer")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Offer)]
	public async Task<Result<AdmissionApplicationDto>> IssueOfferAsync(Guid id, AdmissionApplicationDecisionDto input) => Result.Success(await service.IssueOfferAsync(id, input));

	[HttpPost("{id:guid}/offer/accept")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Offer)]
	public async Task<Result<AdmissionApplicationDto>> AcceptOfferAsync(Guid id, AdmissionApplicationDecisionDto input) => Result.Success(await service.AcceptOfferAsync(id, input));

	[HttpPost("{id:guid}/offer/decline")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Offer)]
	public async Task<Result<AdmissionApplicationDto>> DeclineOfferAsync(Guid id, AdmissionApplicationDecisionDto input) => Result.Success(await service.DeclineOfferAsync(id, input));

	[HttpGet("{id:guid}/finalization-preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	public async Task<Result<AdmissionFinalizationPreviewDto>> GetFinalizationPreviewAsync(Guid id) =>
		Result.Success(await service.GetFinalizationPreviewAsync(id));

	[HttpPost("{id:guid}/finalize")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Finalize)]
	public async Task<Result<AdmissionFinalizationResultDto>> FinalizeAsync(Guid id, FinalizeAdmissionApplicationDto input) =>
		Result.Success(await service.FinalizeAsync(id, input));

	[Obsolete("Admissions no longer creates operational enrollment. Use /finalize.")]
	[HttpPost("{id:guid}/enroll")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Finalize)]
	public async Task<Result<AdmissionEnrollmentResultDto>> EnrollAsync(Guid id, EnrollAdmissionApplicationDto input) => Result.Success(await service.EnrollAsync(id, input));

	[Obsolete("Placement is owned by Student Onboarding.")]
	[HttpGet("{id:guid}/enrollment-options")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Finalize)]
	public async Task<Result<AdmissionEnrollmentOptionsDto>> GetEnrollmentOptionsAsync(Guid id) => Result.Success(await service.GetEnrollmentOptionsAsync(id));
}

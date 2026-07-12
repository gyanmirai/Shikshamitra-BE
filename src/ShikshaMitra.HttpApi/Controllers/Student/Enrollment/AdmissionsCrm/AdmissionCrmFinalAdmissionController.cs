using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.AdmissionsCrm;

[ApiController]
[Authorize]
[Route("api/student-enrollment/admissions-crm")]
public class AdmissionCrmFinalAdmissionController(
	IAdmissionCrmFinalAdmissionAppService finalAdmissionAppService) : AbpControllerBase
{
	[HttpPost("{academicYearId}/final-admission/seat-pools")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageSeatPlans)]
	public Task<AdmissionCrmSeatPoolDto> CreateSeatPoolAsync(
		Guid academicYearId, [FromBody] UpsertAdmissionCrmSeatPoolDto input) =>
		finalAdmissionAppService.CreateSeatPoolAsync(academicYearId, input);

	[HttpPut("final-admission/seat-pools/{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageSeatPlans)]
	public Task<AdmissionCrmSeatPoolDto> UpdateSeatPoolAsync(
		Guid id, [FromBody] UpsertAdmissionCrmSeatPoolDto input) =>
		finalAdmissionAppService.UpdateSeatPoolAsync(id, input);

	[HttpGet("{academicYearId}/final-admission/seat-pools")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewSeatPlans)]
	public Task<PagedResultDto<AdmissionCrmSeatPoolDto>> GetSeatPoolsAsync(
		Guid academicYearId, [FromQuery] GetAdmissionCrmSeatPoolListInput input) =>
		finalAdmissionAppService.GetSeatPoolsAsync(academicYearId, input);

	[HttpPost("leads/{leadId}/final-admission/offers")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOffers)]
	public Task<AdmissionCrmSeatOfferDto> CreateOfferAsync(
		Guid leadId, [FromBody] CreateAdmissionCrmSeatOfferDto input) =>
		finalAdmissionAppService.CreateOfferAsync(leadId, input);

	[HttpGet("{academicYearId}/final-admission/offers")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<PagedResultDto<AdmissionCrmSeatOfferDto>> GetOffersAsync(
		Guid academicYearId, [FromQuery] GetAdmissionCrmSeatOfferListInput input) =>
		finalAdmissionAppService.GetOffersAsync(academicYearId, input);

	[HttpGet("final-admission/offers/{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmSeatOfferDto> GetOfferAsync(Guid id) =>
		finalAdmissionAppService.GetOfferAsync(id);

	[HttpPost("final-admission/offers/{id}/submit")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOffers)]
	public Task<AdmissionCrmSeatOfferDto> SubmitOfferAsync(Guid id) =>
		finalAdmissionAppService.SubmitOfferAsync(id);

	[HttpPost("final-admission/offers/{id}/approve")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ApproveOffers)]
	public Task<AdmissionCrmSeatOfferDto> ApproveOfferAsync(Guid id) =>
		finalAdmissionAppService.ApproveOfferAsync(id);

	[HttpPost("final-admission/offers/{id}/issue")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageSeatReservations)]
	public Task<AdmissionCrmSeatOfferDto> IssueOfferAsync(
		Guid id, [FromBody] IssueAdmissionCrmSeatOfferDto input) =>
		finalAdmissionAppService.IssueOfferAsync(id, input);

	[HttpPost("final-admission/offers/{id}/accept")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOffers)]
	public Task<AdmissionCrmSeatOfferDto> AcceptOfferAsync(
		Guid id, [FromBody] RespondAdmissionCrmSeatOfferDto input) =>
		finalAdmissionAppService.AcceptOfferAsync(id, input);

	[HttpPost("final-admission/offers/{id}/decline")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOffers)]
	public Task<AdmissionCrmSeatOfferDto> DeclineOfferAsync(
		Guid id, [FromBody] RespondAdmissionCrmSeatOfferDto input) =>
		finalAdmissionAppService.DeclineOfferAsync(id, input);

	[HttpPost("final-admission/offers/{id}/revoke")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOffers)]
	public Task<AdmissionCrmSeatOfferDto> RevokeOfferAsync(
		Guid id, [FromBody] AdmissionCrmReasonDto input) =>
		finalAdmissionAppService.RevokeOfferAsync(id, input);

	[HttpPost("final-admission/offers/{id}/supersede")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOffers)]
	public Task<AdmissionCrmSeatOfferDto> SupersedeOfferAsync(
		Guid id, [FromBody] SupersedeAdmissionCrmSeatOfferDto input) =>
		finalAdmissionAppService.SupersedeOfferAsync(id, input);

	[HttpPost("final-admission/offers/{id}/expire")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOffers)]
	public Task<AdmissionCrmSeatOfferDto> ExpireOfferAsync(Guid id) =>
		finalAdmissionAppService.ExpireOfferAsync(id);

	[HttpPost("{academicYearId}/final-admission/offers/run-expiry")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageAutomation)]
	public Task<AdmissionCrmOfferExpiryRunResultDto> RunOfferExpiryAsync(Guid academicYearId) =>
		finalAdmissionAppService.RunOfferExpiryAsync(academicYearId);

	[HttpPost("leads/{leadId}/final-admission/waitlist")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageWaitlist)]
	public Task<AdmissionCrmWaitlistEntryDto> AddToWaitlistAsync(
		Guid leadId, [FromBody] CreateAdmissionCrmWaitlistEntryDto input) =>
		finalAdmissionAppService.AddToWaitlistAsync(leadId, input);

	[HttpGet("final-admission/seat-pools/{seatPoolId}/waitlist")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewSeatPlans)]
	public Task<IReadOnlyList<AdmissionCrmWaitlistEntryDto>> GetWaitlistAsync(Guid seatPoolId) =>
		finalAdmissionAppService.GetWaitlistAsync(seatPoolId);

	[HttpPost("final-admission/waitlist/{entryId}/prepare-promotion")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageWaitlist)]
	public Task<AdmissionCrmSeatOfferDto> PrepareWaitlistPromotionAsync(
		Guid entryId, [FromBody] PrepareAdmissionCrmWaitlistPromotionDto input) =>
		finalAdmissionAppService.PrepareWaitlistPromotionAsync(entryId, input);

	[HttpPost("final-admission/waitlist/{entryId}/withdraw")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageWaitlist)]
	public Task<AdmissionCrmWaitlistEntryDto> WithdrawFromWaitlistAsync(
		Guid entryId, [FromBody] AdmissionCrmReasonDto input) =>
		finalAdmissionAppService.WithdrawFromWaitlistAsync(entryId, input);

	[HttpPut("final-admission/waitlist/{entryId}/governance")]
	public Task<AdmissionCrmWaitlistEntryDto> UpdateWaitlistGovernanceAsync(
		Guid entryId, [FromBody] UpdateAdmissionCrmWaitlistGovernanceDto input) =>
		finalAdmissionAppService.UpdateWaitlistGovernanceAsync(entryId, input);

	[HttpGet("final-admission/waitlist/{entryId}/history")]
	public Task<IReadOnlyList<AdmissionCrmWaitlistHistoryDto>> GetWaitlistHistoryAsync(Guid entryId) =>
		finalAdmissionAppService.GetWaitlistHistoryAsync(entryId);

	[HttpPost("{academicYearId}/final-admission/waitlist/run-promotion")]
	public Task<AdmissionCrmWaitlistPromotionRunResultDto> RunWaitlistPromotionAsync(
		Guid academicYearId, [FromBody] RunAdmissionCrmWaitlistPromotionDto input) =>
		finalAdmissionAppService.RunWaitlistPromotionAsync(academicYearId, input);

	[HttpPut("leads/{leadId}/final-admission/decision")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.MakeAdmissionDecision)]
	public Task<AdmissionCrmAdmissionDecisionDto> RecordDecisionAsync(
		Guid leadId, [FromBody] RecordAdmissionCrmAdmissionDecisionDto input) =>
		finalAdmissionAppService.RecordDecisionAsync(leadId, input);

	[HttpGet("leads/{leadId}/final-admission/decision")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmAdmissionDecisionDto?> GetDecisionAsync(Guid leadId) =>
		finalAdmissionAppService.GetDecisionAsync(leadId);

	[HttpGet("leads/{leadId}/final-admission/decisions")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<IReadOnlyList<AdmissionCrmAdmissionDecisionDto>> GetDecisionHistoryAsync(Guid leadId) =>
		finalAdmissionAppService.GetDecisionHistoryAsync(leadId);
}

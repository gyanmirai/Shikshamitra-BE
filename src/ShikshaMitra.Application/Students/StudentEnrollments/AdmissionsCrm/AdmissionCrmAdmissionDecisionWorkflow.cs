using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmAdmissionDecisionWorkflow(
	IRepository<AdmissionCrmAdmissionDecision, Guid> decisionRepository,
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	IRepository<AdmissionCrmSeatOffer, Guid> offerRepository,
	AdmissionCrmFinalAdmissionAccess access,
	AdmissionCrmOfferReadinessWorkflow readiness,
	AdmissionCrmOfferReservationWorkflow reservations,
	AdmissionCrmStructuredReviewDecisionWorkflow reviews,
	AdmissionCrmFinalAdmissionTimeline timeline) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmAdmissionDecisionDto> RecordAsync(Guid leadId, RecordAdmissionCrmAdmissionDecisionDto input)
	{
		var lead = await access.GetLeadAsync(leadId, "RecordDecisionAsync");
		if (input.OverrideUsed && !await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.OverrideEligibility))
			throw new AbpAuthorizationException("Admission-decision override permission is required.");
		if (input.Status == AdmissionCrmAdmissionDecisionStatus.Cancelled && !await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.CancelAdmission))
			throw new AbpAuthorizationException("Admission-cancellation permission is required.");
		AdmissionCrmSeatOffer? offer = null;
		if (input.OfferId.HasValue)
		{
			offer = await access.GetOfferAsync(input.OfferId.Value, "RecordDecisionAsync");
			if (offer.LeadId != leadId || (input.SeatPoolId.HasValue && offer.SeatPoolId != input.SeatPoolId.Value))
				throw new BusinessException("AdmissionCrm:AdmissionDecisionOfferMismatch");
		}
		if (input.Status == AdmissionCrmAdmissionDecisionStatus.Approved && !input.OverrideUsed &&
			(offer == null || offer.Status != AdmissionCrmSeatOfferStatus.Accepted))
			throw new BusinessException("AdmissionCrm:AcceptedOfferRequiredForApproval");
		if (input.Status == AdmissionCrmAdmissionDecisionStatus.Approved && !input.OverrideUsed)
			await readiness.EnsurePaymentAsync(leadId, AdmissionCrmPaymentGateMilestone.FinalApproval);
		if (input.Status == AdmissionCrmAdmissionDecisionStatus.Cancelled && offer == null)
			throw new BusinessException("AdmissionCrm:AcceptedOfferRequiredForCancellation");
		var review = await reviews.EnsureReadyAsync(lead, input);
		var queryable = await decisionRepository.GetQueryableAsync();
		var previous = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.LeadId == leadId).OrderByDescending(x => x.DecidedAt));
		if (previous != null && !input.OverrideUsed) throw new BusinessException("AdmissionCrm:AdmissionDecisionOverrideRequired");
		var userId = CurrentUser.Id ?? throw new AbpAuthorizationException("An authenticated user is required.");
		var decision = new AdmissionCrmAdmissionDecision(GuidGenerator.Create(), CurrentTenant.Id, lead.AcademicYearId, lead.Id);
		decision.Decide(input.Status, input.SeatPoolId ?? offer?.SeatPoolId, input.OfferId, input.Reason,
			input.OverrideUsed, input.OverrideReason, userId, Clock.Now);
		if (input.Status == AdmissionCrmAdmissionDecisionStatus.Cancelled && offer?.Status == AdmissionCrmSeatOfferStatus.Accepted)
		{
			offer.CancelAccepted(input.Reason, Clock.Now);
			await reservations.ReleaseAsync(offer, input.Reason, true);
			await offerRepository.UpdateAsync(offer, autoSave: true);
		}
		ApplyToLead(lead, input.Status, input.Reason);
		await decisionRepository.InsertAsync(decision, autoSave: true);
		await leadRepository.UpdateAsync(lead, autoSave: true);
		await reviews.CloseIfReadyAsync(review, lead, input.Status, userId);
		await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.AdmissionDecisionRecorded,
			"Admission decision recorded", $"Decision: {input.Status}. Reason: {input.Reason}", decision.Id);
		return AdmissionCrmFinalAdmissionMapper.Decision(decision);
	}

	private void ApplyToLead(AdmissionInquiryLead lead, AdmissionCrmAdmissionDecisionStatus status, string reason)
	{
		switch (status)
		{
			case AdmissionCrmAdmissionDecisionStatus.Approved: lead.ChangeStage(AdmissionInquiryStage.Eligible); break;
			case AdmissionCrmAdmissionDecisionStatus.Rejected: lead.ChangeStage(AdmissionInquiryStage.Rejected); break;
			case AdmissionCrmAdmissionDecisionStatus.Waitlisted: lead.ChangeStage(AdmissionInquiryStage.Waitlisted); break;
			case AdmissionCrmAdmissionDecisionStatus.Withdrawn:
			case AdmissionCrmAdmissionDecisionStatus.Expired:
			case AdmissionCrmAdmissionDecisionStatus.Cancelled: lead.Archive(reason, Clock.Now); break;
		}
	}
}

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

public class AdmissionCrmStructuredReviewDecisionWorkflow(
	IRepository<AdmissionCrmReviewConfiguration, Guid> configurationRepository,
	IRepository<AdmissionCrmStructuredReview, Guid> reviewRepository,
	AdmissionCrmFinalAdmissionTimeline timeline) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmStructuredReview?> EnsureReadyAsync(AdmissionInquiryLead lead, RecordAdmissionCrmAdmissionDecisionDto input)
	{
		if (input.Status is not (AdmissionCrmAdmissionDecisionStatus.Approved or AdmissionCrmAdmissionDecisionStatus.Rejected or
			AdmissionCrmAdmissionDecisionStatus.Waitlisted)) return null;
		var configurations = await configurationRepository.GetQueryableAsync();
		var configuration = await AsyncExecuter.FirstOrDefaultAsync(configurations.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == lead.AcademicYearId));
		if (configuration?.IsEnabled != true) return null;
		if (!input.OverrideUsed && !await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewConfidentialReviews))
			throw new AbpAuthorizationException("Confidential structured-review access is required to record this admission decision.");
		var queryable = await reviewRepository.WithDetailsAsync(x => x.Criteria, x => x.Assignments);
		var review = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id));
		if (input.OverrideUsed) return review;
		if (review == null) throw new BusinessException("AdmissionCrm:StructuredReviewRequiredForDecision");
		if (review.Status is not (AdmissionCrmStructuredReviewStatus.ReadyForDecision or AdmissionCrmStructuredReviewStatus.Closed))
			throw new BusinessException("AdmissionCrm:StructuredReviewNotReadyForDecision")
				.WithData("SubmittedReviewCount", review.SubmittedReviewCount)
				.WithData("RequiredCompletedReviews", review.RequiredCompletedReviews);
		return review;
	}

	public async Task CloseIfReadyAsync(AdmissionCrmStructuredReview? review, AdmissionInquiryLead lead,
		AdmissionCrmAdmissionDecisionStatus status, Guid userId)
	{
		if (review?.Status != AdmissionCrmStructuredReviewStatus.ReadyForDecision) return;
		var reason = $"Final admission decision recorded as {status}.";
		review.Close(userId, Clock.Now, reason);
		await reviewRepository.UpdateAsync(review, autoSave: true);
		await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.StructuredReviewClosed,
			"Structured admissions review closed", reason, review.Id);
	}
}

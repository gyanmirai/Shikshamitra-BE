using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmActivationApplicantReadiness(
	IRepository<AdmissionCrmAdmissionDecision, Guid> decisionRepository,
	IRepository<AdmissionCrmSeatOffer, Guid> offerRepository,
	IRepository<AdmissionCrmAdmissionQuotation, Guid> quotationRepository,
	IRepository<AdmissionCrmApplicantIdentityProfile, Guid> identityRepository,
	IRepository<AdmissionCrmDocumentChecklistAssignment, Guid> documentRepository)
	: ApplicationService, ITransientDependency
{
	public async Task EvaluateAsync(
		AdmissionInquiryLead lead,
		PreviewAdmissionCrmActivationDto input,
		List<AdmissionCrmActivationReadinessItemDto> items)
	{
		var decisionQuery = await decisionRepository.GetQueryableAsync();
		var decisions = await AsyncExecuter.ToListAsync(decisionQuery.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id));
		var decision = decisions.OrderByDescending(x => x.DecidedAt).FirstOrDefault();
		Add(items, "admission_decision", "Final admission approval",
			decision?.Status == AdmissionCrmAdmissionDecisionStatus.Approved,
			decision?.Reason ?? "Final approval is missing.");

		var offerAccepted = true;
		if (decision?.OfferId.HasValue == true)
		{
			var offer = await offerRepository.FindAsync(decision.OfferId.Value);
			offerAccepted = offer?.Status == AdmissionCrmSeatOfferStatus.Accepted;
		}
		Add(items, "seat_offer", "Offer and seat commitment", offerAccepted,
			offerAccepted ? "Offer route is satisfied." : "The linked offer is not accepted.");

		var quoteQuery = await quotationRepository.GetQueryableAsync();
		var quotes = await AsyncExecuter.ToListAsync(quoteQuery.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id && x.Status == AdmissionCrmQuotationStatus.Issued));
		var quote = quotes.OrderByDescending(x => x.Version).FirstOrDefault();
		var financialReady = quote == null ||
			quote.GateMilestone > AdmissionCrmPaymentGateMilestone.StudentActivation || quote.IsGateSatisfiedAt(Clock.Now);
		Add(items, "payment_gate", "Activation payment gate", financialReady,
			financialReady ? "Financial condition is satisfied or not required." : "Finance has not confirmed the activation gate.");

		var identityQuery = await identityRepository.GetQueryableAsync();
		var identity = await AsyncExecuter.FirstOrDefaultAsync(identityQuery.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id));
		var identityReady = identity?.Status is AdmissionCrmIdentityResolutionStatus.Ready or AdmissionCrmIdentityResolutionStatus.Overridden;
		Add(items, "identity", "Household and identity", identityReady,
			identityReady ? "Identity resolution is complete." : "Household identity resolution is not ready.");
		if (input.RequireGuardianAccount)
			Add(items, "guardian_access", "Guardian access", identity?.AccountLinkReady == true,
				identity?.AccountLinkReady == true ? "Guardian portal account is linked." : "A required guardian portal account is not linked.");

		var documentQuery = await documentRepository.GetQueryableAsync();
		var documents = await AsyncExecuter.ToListAsync(documentQuery.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id));
		var current = documents.OrderByDescending(x => x.AssignedAt).FirstOrDefault();
		var documentsReady = current == null || current.ReadinessConfirmedAt.HasValue;
		Add(items, "documents", "Document readiness", documentsReady,
			documentsReady ? "Document checklist is ready or not applicable." : "Document readiness is not confirmed.");
	}

	private static void Add(List<AdmissionCrmActivationReadinessItemDto> items, string key, string label, bool passed, string detail) =>
		items.Add(new AdmissionCrmActivationReadinessItemDto
		{
			Key = key, Label = label, Blocking = true, Detail = detail,
			Status = passed ? AdmissionCrmActivationStepStatus.Completed : AdmissionCrmActivationStepStatus.Failed
		});
}

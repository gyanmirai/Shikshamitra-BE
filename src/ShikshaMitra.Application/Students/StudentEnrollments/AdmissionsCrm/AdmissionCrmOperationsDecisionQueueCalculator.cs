using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmOperationsDecisionQueueCalculator
{
	internal static List<AdmissionCrmOperationsQueueItemDto> BuildRouteQueue(AdmissionCrmOperationsData data, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads) =>
		data.Routes.Where(x => x.IsPrimary && x.Status is AdmissionCrmRouteEligibilityStatus.PendingReview or AdmissionCrmRouteEligibilityStatus.EvidencePending)
			.Where(x => leads.ContainsKey(x.LeadId)).Select(x => CreateItem(leads[x.LeadId], $"route:{x.Id}",
				"Admission route requires review", x.EvaluationReason ?? x.AssignmentReason, x.Status.ToString(), null,
				AdmissionCrmOperationsFormat.AgeDays(x.AssignedAt, data.Now), x.Status == AdmissionCrmRouteEligibilityStatus.EvidencePending)).ToList();

	internal static List<AdmissionCrmOperationsQueueItemDto> BuildRteQueue(AdmissionCrmOperationsData data, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads) =>
		data.Routes.Where(x => x.IsPrimary && x.RouteType == AdmissionCrmRouteType.RteEws && x.Status is not (AdmissionCrmRouteEligibilityStatus.Eligible or AdmissionCrmRouteEligibilityStatus.ExceptionApproved))
			.Where(x => leads.ContainsKey(x.LeadId)).Select(x => CreateItem(leads[x.LeadId], $"rte:{x.Id}",
				"Government allotment needs reconciliation", x.AssignmentReason, x.Status.ToString(), null, AdmissionCrmOperationsFormat.AgeDays(x.AssignedAt, data.Now), true)).ToList();

	internal static List<AdmissionCrmOperationsQueueItemDto> BuildTransferQueue(AdmissionCrmOperationsData data, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads) =>
		data.Routes.Where(x => x.IsPrimary && x.RouteType is AdmissionCrmRouteType.Transfer or AdmissionCrmRouteType.Lateral or AdmissionCrmRouteType.Interstate && x.Status == AdmissionCrmRouteEligibilityStatus.EvidencePending)
			.Where(x => leads.ContainsKey(x.LeadId)).Select(x => CreateItem(leads[x.LeadId], $"transfer:{x.Id}",
				"Transfer evidence is pending", x.EvaluationReason, x.Status.ToString(), null, AdmissionCrmOperationsFormat.AgeDays(x.AssignedAt, data.Now), true)).ToList();

	internal static List<AdmissionCrmOperationsQueueItemDto> BuildActivationRepairQueue(AdmissionCrmOperationsData data, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads) =>
		data.Activations.Where(x => x.Status == AdmissionCrmActivationStatus.RepairRequired).Where(x => leads.ContainsKey(x.LeadId))
			.Select(x => CreateItem(leads[x.LeadId], $"activation:{x.Id}", "Activation repair required", x.RepairReason,
				x.RepairStep ?? x.Status.ToString(), null, AdmissionCrmOperationsFormat.AgeDays(x.LastModificationTime ?? x.RequestedAt, data.Now), true)).ToList();

	internal static List<AdmissionCrmOperationsQueueItemDto> BuildCancellationReviewQueue(AdmissionCrmOperationsData data, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads) =>
		data.Activations.Where(x => x.Status == AdmissionCrmActivationStatus.CancellationPending).Where(x => leads.ContainsKey(x.LeadId))
			.Select(x => CreateItem(leads[x.LeadId], $"activation-cancellation:{x.Id}", "Activation cancellation requires approval",
				x.CancellationReason, x.Status.ToString(), null, AdmissionCrmOperationsFormat.AgeDays(x.CancellationRequestedAt ?? x.RequestedAt, data.Now), true)).ToList();

	internal static List<AdmissionCrmOperationsQueueItemDto> BuildRouteAppealReviewQueue(AdmissionCrmOperationsData data, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads) =>
		data.RouteAppeals.Where(x => x.Status is AdmissionCrmRouteAppealStatus.Requested or AdmissionCrmRouteAppealStatus.UnderReview)
			.Where(x => leads.ContainsKey(x.LeadId)).Select(x => CreateItem(leads[x.LeadId], $"route-appeal:{x.Id}",
				"Admission route appeal requires review", x.Reason, x.RequestedStatus.ToString(), null,
				AdmissionCrmOperationsFormat.AgeDays(x.RequestedAt, data.Now), true)).ToList();

	internal static List<AdmissionCrmOperationsQueueItemDto> BuildServiceHandoffExceptionQueue(
		AdmissionCrmOperationsData data, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads) =>
		data.ServiceHandoffs.Where(x => x.Status is AdmissionCrmServiceHandoffStatus.Pending or AdmissionCrmServiceHandoffStatus.Failed)
			.Where(x => leads.ContainsKey(x.LeadId)).Select(x => CreateItem(leads[x.LeadId], $"service-handoff:{x.Id}",
				$"{x.Type} service handoff needs attention", x.LastError ?? $"Dispatch to {x.DestinationModule} is pending.",
				x.Status.ToString(), null, AdmissionCrmOperationsFormat.AgeDays(x.RequestedAt, data.Now), x.Status == AdmissionCrmServiceHandoffStatus.Failed,
				serviceHandoffId: x.Id)).ToList();

	internal static List<AdmissionCrmOperationsQueueItemDto> BuildPolicyExceptionQueue(AdmissionCrmOperationsData data, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads) =>
		data.PolicyExceptions.Where(x => x.Status == AdmissionCrmPolicyExceptionStatus.ApprovalPending)
			.Where(x => leads.ContainsKey(x.LeadId)).Select(x => CreateItem(leads[x.LeadId], $"policy-exception:{x.Id}",
				"Policy exception requires approval", x.Reason, x.Status.ToString(), x.ExpiresAt.HasValue ? AdmissionCrmOperationsFormat.ToOffset(x.ExpiresAt.Value) : null,
				AdmissionCrmOperationsFormat.AgeDays(x.RequestedAt, data.Now), !x.ExpiresAt.HasValue || x.ExpiresAt.Value <= data.Now.AddDays(2))).ToList();

	internal static List<AdmissionCrmOperationsQueueItemDto> BuildPaymentConditionQueue(AdmissionCrmOperationsData data, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads) =>
		data.Quotations.Where(x => x.Status == AdmissionCrmQuotationStatus.Issued && !x.IsGateSatisfiedAt(data.Now))
			.GroupBy(x => x.LeadId).Select(x => x.OrderByDescending(q => q.Version).First())
			.Where(x => leads.ContainsKey(x.LeadId)).Select(x => CreateItem(leads[x.LeadId], $"payment:{x.Id}",
				"Admission payment condition is pending", $"{x.Currency} {Math.Max(0, x.RequiredAmount - x.ConfirmedAmount):0.00} remains against {x.QuotationNumber}.",
				x.FinanceState.ToString(), AdmissionCrmOperationsFormat.ToOffset(x.ValidUntil), AdmissionCrmOperationsFormat.AgeDays(x.IssuedAt ?? x.CreationTime, data.Now), x.ValidUntil <= data.Now.AddDays(2))).ToList();

	internal static List<AdmissionCrmOperationsQueueItemDto> BuildHouseholdResolutionQueue(AdmissionCrmOperationsData data, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads)
	{
		var profiles = data.IdentityProfiles.ToDictionary(x => x.LeadId);
		return data.Leads.Where(x => !profiles.TryGetValue(x.Id, out var profile) ||
			profile.Status is AdmissionCrmIdentityResolutionStatus.Unresolved or AdmissionCrmIdentityResolutionStatus.PendingReview)
			.Select(x => CreateItem(x, $"household:{x.Id}", "Household and guardian resolution is pending",
				profiles.GetValueOrDefault(x.Id)?.ConflictReason, profiles.GetValueOrDefault(x.Id)?.Status.ToString() ?? "NotStarted",
				null, AdmissionCrmOperationsFormat.AgeDays(x.LastActivityTime, data.Now), x.Stage >= AdmissionInquiryStage.Eligible)).ToList();
	}

	internal static List<AdmissionCrmOperationsQueueItemDto> BuildIdentityConflictQueue(AdmissionCrmOperationsData data, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads) =>
		data.IdentityProfiles.Where(x => x.Status == AdmissionCrmIdentityResolutionStatus.Conflict && leads.ContainsKey(x.LeadId))
			.Select(x => CreateItem(leads[x.LeadId], $"identity-conflict:{x.Id}", "Identity conflict requires resolution",
				x.ConflictReason, x.Status.ToString(), null, AdmissionCrmOperationsFormat.AgeDays(x.LastModificationTime ?? x.CreationTime, data.Now), true)).ToList();

	internal static List<AdmissionCrmOperationsQueueItemDto> BuildFinalApprovalQueue(AdmissionCrmOperationsData data, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads)
	{
		var decidedLeadIds = data.Decisions.Where(x => x.Status != AdmissionCrmAdmissionDecisionStatus.Pending).Select(x => x.LeadId).ToHashSet();
		return data.Registrations.Where(x => x.Status == AdmissionCrmRegistrationStatus.AdmissionReady && !decidedLeadIds.Contains(x.LeadId))
			.Where(x => leads.ContainsKey(x.LeadId)).Select(x => CreateItem(leads[x.LeadId], $"final-approval:{x.Id}",
				"Final admission approval is pending", $"Form {x.FormNumber} is admission ready.", "Pending",
				null, AdmissionCrmOperationsFormat.AgeDays(x.VerifiedAt ?? x.LastModificationTime ?? x.IssuedAt, data.Now), true, registrationApplicationId: x.Id)).ToList();
	}

	internal static List<AdmissionCrmOperationsQueueItemDto> BuildReturningIdentityMatchQueue(AdmissionCrmOperationsData data, IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads) =>
		data.Routes.Where(x => x.IsPrimary && x.RouteType == AdmissionCrmRouteType.ReturningStudent &&
			x.Status is AdmissionCrmRouteEligibilityStatus.PendingReview or AdmissionCrmRouteEligibilityStatus.EvidencePending)
			.Where(x => leads.ContainsKey(x.LeadId)).Select(x => CreateItem(leads[x.LeadId], $"returning:{x.Id}",
				"Former student identity match requires review", x.EvaluationReason ?? x.AssignmentReason, x.Status.ToString(),
				null, AdmissionCrmOperationsFormat.AgeDays(x.AssignedAt, data.Now), true)).ToList();

	internal static IEnumerable<AdmissionCrmApplicantDocument> GetCurrentDocuments(
		IEnumerable<AdmissionCrmApplicantDocument> documents)
	{
		return documents
			.Where(x => x.SupersededAt == null)
			.GroupBy(x => new { x.ChecklistAssignmentId, x.RequirementKey })
			.Select(x => x.OrderByDescending(document => document.Version).First());
	}

	internal static AdmissionCrmOperationsQueueItemDto CreateItem(
		AdmissionInquiryLead lead,
		string key,
		string title,
		string? details,
		string status,
		DateTimeOffset? dueAt,
		int ageDays,
		bool urgent,
		Guid? examRecordId = null,
		Guid? registrationApplicationId = null,
		Guid? documentId = null,
		Guid? checklistAssignmentId = null,
		Guid? serviceHandoffId = null)
	{
		return new AdmissionCrmOperationsQueueItemDto
		{
			Key = key,
			LeadId = lead.Id,
			ApplicantNumber = lead.ApplicantNumber,
			ExamRecordId = examRecordId,
			RegistrationApplicationId = registrationApplicationId,
			DocumentId = documentId,
			ChecklistAssignmentId = checklistAssignmentId,
			ServiceHandoffId = serviceHandoffId,
			ApplicantName = AdmissionCrmOperationsFormat.GetApplicantName(lead),
			Title = title,
			Details = details,
			Status = status,
			DueAt = dueAt,
			AgeDays = ageDays,
			IsUrgent = urgent,
			OwnerUserId = lead.OwnerUserId
		};
	}
}
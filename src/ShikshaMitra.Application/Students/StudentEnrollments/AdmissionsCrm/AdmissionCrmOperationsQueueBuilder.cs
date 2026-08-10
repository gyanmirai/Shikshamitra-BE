using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmOperationsQueueBuilder
{
	internal static IReadOnlyList<AdmissionCrmOperationsQueueItemDto> BuildQueueItems(
		AdmissionCrmOperationsData data,
		AdmissionCrmOperationsQueueType queueType)
	{
		var leads = data.Leads.ToDictionary(x => x.Id);
		var items = queueType switch
		{
			AdmissionCrmOperationsQueueType.UnassignedExam => AdmissionCrmOperationsApplicantQueueCalculator.BuildUnassignedExamQueue(data, leads),
			AdmissionCrmOperationsQueueType.CapacityConflict => AdmissionCrmOperationsApplicantQueueCalculator.BuildCapacityConflictQueue(data, leads),
			AdmissionCrmOperationsQueueType.NoShow => AdmissionCrmOperationsApplicantQueueCalculator.BuildNoShowQueue(data, leads),
			AdmissionCrmOperationsQueueType.RegistrationInvitationPending => AdmissionCrmOperationsApplicantQueueCalculator.BuildRegistrationInvitationPendingQueue(data),
			AdmissionCrmOperationsQueueType.PendingPaperEntry => AdmissionCrmOperationsApplicantQueueCalculator.BuildPendingPaperEntryQueue(data, leads),
			AdmissionCrmOperationsQueueType.ExpiringRegistrationLink => AdmissionCrmOperationsApplicantQueueCalculator.BuildExpiringLinkQueue(data, leads),
			AdmissionCrmOperationsQueueType.IncompleteRegistration => AdmissionCrmOperationsApplicantQueueCalculator.BuildIncompleteRegistrationQueue(data, leads),
			AdmissionCrmOperationsQueueType.RejectedDocument => AdmissionCrmOperationsApplicantQueueCalculator.BuildDocumentQueue(data, leads, AdmissionCrmApplicantDocumentStatus.Rejected),
			AdmissionCrmOperationsQueueType.DocumentVerificationBacklog => AdmissionCrmOperationsApplicantQueueCalculator.BuildDocumentReviewQueue(data, leads),
			AdmissionCrmOperationsQueueType.AdmissionReady => AdmissionCrmOperationsApplicantQueueCalculator.BuildAdmissionReadyQueue(data, leads),
			AdmissionCrmOperationsQueueType.PolicyException => AdmissionCrmOperationsDecisionQueueCalculator.BuildPolicyExceptionQueue(data, leads),
			AdmissionCrmOperationsQueueType.PaymentCondition => AdmissionCrmOperationsDecisionQueueCalculator.BuildPaymentConditionQueue(data, leads),
			AdmissionCrmOperationsQueueType.HouseholdResolution => AdmissionCrmOperationsDecisionQueueCalculator.BuildHouseholdResolutionQueue(data, leads),
			AdmissionCrmOperationsQueueType.IdentityConflict => AdmissionCrmOperationsDecisionQueueCalculator.BuildIdentityConflictQueue(data, leads),
			AdmissionCrmOperationsQueueType.FinalApproval => AdmissionCrmOperationsDecisionQueueCalculator.BuildFinalApprovalQueue(data, leads),
			AdmissionCrmOperationsQueueType.RouteEligibilityReview => AdmissionCrmOperationsDecisionQueueCalculator.BuildRouteQueue(data, leads),
			AdmissionCrmOperationsQueueType.GovernmentAllotmentReconciliation => AdmissionCrmOperationsDecisionQueueCalculator.BuildRteQueue(data, leads),
			AdmissionCrmOperationsQueueType.TransferEvidencePending => AdmissionCrmOperationsDecisionQueueCalculator.BuildTransferQueue(data, leads),
			AdmissionCrmOperationsQueueType.ActivationRepair => AdmissionCrmOperationsDecisionQueueCalculator.BuildActivationRepairQueue(data, leads),
			AdmissionCrmOperationsQueueType.ReturningIdentityMatch => AdmissionCrmOperationsDecisionQueueCalculator.BuildReturningIdentityMatchQueue(data, leads),
			AdmissionCrmOperationsQueueType.CancellationReview => AdmissionCrmOperationsDecisionQueueCalculator.BuildCancellationReviewQueue(data, leads),
			AdmissionCrmOperationsQueueType.RouteAppealReview => AdmissionCrmOperationsDecisionQueueCalculator.BuildRouteAppealReviewQueue(data, leads),
			AdmissionCrmOperationsQueueType.ServiceHandoffException => AdmissionCrmOperationsDecisionQueueCalculator.BuildServiceHandoffExceptionQueue(data, leads),
			_ => []
		};

		AdmissionCrmOperationsQueueContextMapper.PopulateQueueContexts(data, queueType, items);

		return items
			.OrderByDescending(x => x.IsUrgent)
			.ThenBy(x => x.DueAt ?? DateTimeOffset.MaxValue)
			.ThenByDescending(x => x.AgeDays)
			.ThenBy(x => x.ApplicantName)
			.ToList();
	}

	internal static bool IsRegistrationQueue(AdmissionCrmOperationsQueueType queueType) => queueType is
		AdmissionCrmOperationsQueueType.RegistrationInvitationPending or
		AdmissionCrmOperationsQueueType.PendingPaperEntry or
		AdmissionCrmOperationsQueueType.ExpiringRegistrationLink or
		AdmissionCrmOperationsQueueType.IncompleteRegistration or
		AdmissionCrmOperationsQueueType.RejectedDocument or
		AdmissionCrmOperationsQueueType.DocumentVerificationBacklog or
		AdmissionCrmOperationsQueueType.AdmissionReady;

	internal static bool IsExamQueue(AdmissionCrmOperationsQueueType queueType) => queueType is
		AdmissionCrmOperationsQueueType.UnassignedExam or
		AdmissionCrmOperationsQueueType.CapacityConflict or
		AdmissionCrmOperationsQueueType.NoShow;

	internal static bool IsOfferQueue(AdmissionCrmOperationsQueueType queueType) => queueType is
		AdmissionCrmOperationsQueueType.FinalApproval or
		AdmissionCrmOperationsQueueType.PolicyException or
		AdmissionCrmOperationsQueueType.PaymentCondition or
		AdmissionCrmOperationsQueueType.RouteEligibilityReview or
		AdmissionCrmOperationsQueueType.GovernmentAllotmentReconciliation or
		AdmissionCrmOperationsQueueType.RouteAppealReview;

	internal static bool IsActivationQueue(AdmissionCrmOperationsQueueType queueType) => queueType is
		AdmissionCrmOperationsQueueType.ActivationRepair or
		AdmissionCrmOperationsQueueType.CancellationReview or
		AdmissionCrmOperationsQueueType.ServiceHandoffException;

	internal static int GetQueueCount(
		IReadOnlyDictionary<AdmissionCrmOperationsQueueType, IReadOnlyList<AdmissionCrmOperationsQueueItemDto>> queues,
		AdmissionCrmOperationsQueueType queueType) =>
		queues.TryGetValue(queueType, out var items) ? items.Count : 0;

	internal static string GetQueueWorkspace(AdmissionCrmOperationsQueueType queueType)
	{
		if (IsRegistrationQueue(queueType)) return "registrations";
		if (IsOfferQueue(queueType)) return "offers";
		if (IsActivationQueue(queueType)) return "activation";
		return queueType is AdmissionCrmOperationsQueueType.UnassignedExam or
			AdmissionCrmOperationsQueueType.CapacityConflict or AdmissionCrmOperationsQueueType.NoShow
			? "exams"
			: "operations";
	}

	internal static string GetQueueLabel(AdmissionCrmOperationsQueueType queueType)
	{
		return queueType switch
		{
			AdmissionCrmOperationsQueueType.UnassignedExam => "Unassigned exams",
			AdmissionCrmOperationsQueueType.CapacityConflict => "Capacity conflicts",
			AdmissionCrmOperationsQueueType.NoShow => "No-shows and overdue attendance",
			AdmissionCrmOperationsQueueType.RegistrationInvitationPending => "Eligible applicants awaiting registration",
			AdmissionCrmOperationsQueueType.PendingPaperEntry => "Pending paper registration entry",
			AdmissionCrmOperationsQueueType.ExpiringRegistrationLink => "Expiring registration links",
			AdmissionCrmOperationsQueueType.IncompleteRegistration => "Incomplete registrations",
			AdmissionCrmOperationsQueueType.RejectedDocument => "Rejected documents",
			AdmissionCrmOperationsQueueType.DocumentVerificationBacklog => "Document verification backlog",
			AdmissionCrmOperationsQueueType.AdmissionReady => "Admission-ready applicants",
			AdmissionCrmOperationsQueueType.PolicyException => "Policy exceptions",
			AdmissionCrmOperationsQueueType.PaymentCondition => "Payment conditions",
			AdmissionCrmOperationsQueueType.HouseholdResolution => "Household resolution",
			AdmissionCrmOperationsQueueType.IdentityConflict => "Identity conflicts",
			AdmissionCrmOperationsQueueType.FinalApproval => "Final admission approvals",
			AdmissionCrmOperationsQueueType.RouteEligibilityReview => "Route eligibility review",
			AdmissionCrmOperationsQueueType.GovernmentAllotmentReconciliation => "Government allotment reconciliation",
			AdmissionCrmOperationsQueueType.TransferEvidencePending => "Transfer evidence pending",
			AdmissionCrmOperationsQueueType.ActivationRepair => "Activation repair",
			AdmissionCrmOperationsQueueType.ReturningIdentityMatch => "Returning-student identity matches",
			AdmissionCrmOperationsQueueType.CancellationReview => "Activation cancellation review",
			AdmissionCrmOperationsQueueType.RouteAppealReview => "Admission route appeal review",
			AdmissionCrmOperationsQueueType.ServiceHandoffException => "Optional service handoff exceptions",
			_ => queueType.ToString()
		};
	}
}
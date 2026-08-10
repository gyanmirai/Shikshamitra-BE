using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Students.Compliance;
using ShikshaMitra.Students.StudentEnrollments.Settings;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmAnalyticsGovernanceCalculator
{
internal static AdmissionCrmPolicyAnalyticsDto BuildPolicyAnalytics(
		IReadOnlyCollection<AdmissionCrmPolicyPack> packs,
		IReadOnlyCollection<AdmissionCrmPolicyAdoption> adoptions,
		IReadOnlyCollection<AdmissionCrmPolicyEvaluation> evaluations,
		IReadOnlyCollection<AdmissionCrmPolicyException> exceptions,
		DateTime now)
	{
		var latestEvaluations = evaluations
			.GroupBy(x => new { x.LeadId, x.PolicyAdoptionId, x.RuleCode })
			.Select(x => x.OrderByDescending(y => y.EvaluatedAt).ThenByDescending(y => y.Id).First())
			.ToList();
		return new AdmissionCrmPolicyAnalyticsDto
		{
			ActiveAdoptionCount = adoptions.Count(x => x.IsActive),
			EvaluationCount = latestEvaluations.Count,
			BlockingFailureCount = latestEvaluations.Count(x =>
				x.Severity is AdmissionCrmPolicyRuleSeverity.Blocking or AdmissionCrmPolicyRuleSeverity.ApprovalRequired &&
				x.Status == AdmissionCrmPolicyEvaluationStatus.NotSatisfied),
			EvidencePendingCount = latestEvaluations.Count(x => x.Status == AdmissionCrmPolicyEvaluationStatus.EvidencePending),
			ApprovedExceptionCount = exceptions.Count(x => x.Status == AdmissionCrmPolicyExceptionStatus.Approved),
			SourceReviewOverdueCount = packs.Count(x => x.Status == AdmissionCrmPolicyPackStatus.Published && x.ReviewDueAt < now)
		};
	}

internal static AdmissionCrmRouteAnalyticsDto BuildRouteAnalytics(
		IReadOnlyCollection<AdmissionCrmRouteAssignment> assignments,
		IReadOnlyCollection<AdmissionCrmRouteAppeal> appeals)
	{
		var current = assignments.Where(x => x.IsPrimary).ToList();
		return new AdmissionCrmRouteAnalyticsDto
		{
			AssignedApplicantCount = current.Count,
			EligibleCount = current.Count(x => x.Status is AdmissionCrmRouteEligibilityStatus.Eligible or AdmissionCrmRouteEligibilityStatus.ExceptionApproved),
			PendingReviewCount = current.Count(x => x.Status == AdmissionCrmRouteEligibilityStatus.PendingReview),
			EvidencePendingCount = current.Count(x => x.Status == AdmissionCrmRouteEligibilityStatus.EvidencePending),
			RouteChangeCount = assignments.Count(x => x.PreviousAssignmentId.HasValue),
			AppealPendingCount = appeals.Count(x => x.Status is AdmissionCrmRouteAppealStatus.Requested or AdmissionCrmRouteAppealStatus.UnderReview),
			AppealApprovedCount = appeals.Count(x => x.Status == AdmissionCrmRouteAppealStatus.Approved),
			ApplicantsByRoute = current.GroupBy(x => x.RouteType.ToString()).ToDictionary(x => x.Key, x => x.Count())
		};
	}

internal static AdmissionCrmActivationAnalyticsDto BuildActivationAnalytics(
		IReadOnlyCollection<AdmissionCrmActivation> activations,
		IReadOnlyCollection<AdmissionCrmIdentifierAllocation> identifiers)
	{
		var completed = activations.Where(x => x.Status == AdmissionCrmActivationStatus.FirstDayReady).ToList();
		return new AdmissionCrmActivationAnalyticsDto
		{
			RequestedCount = activations.Count,
			ApprovalPendingCount = activations.Count(x => x.Status == AdmissionCrmActivationStatus.ApprovalPending),
			FirstDayReadyCount = completed.Count,
			RepairRequiredCount = activations.Count(x => x.Status == AdmissionCrmActivationStatus.RepairRequired),
			CancellationPendingCount = activations.Count(x => x.Status == AdmissionCrmActivationStatus.CancellationPending),
			CancelledCount = activations.Count(x => x.Status == AdmissionCrmActivationStatus.Cancelled),
			FirstPassSuccessCount = completed.Count(x => x.AttemptCount == 1),
			FirstPassSuccessRate = AdmissionCrmAnalyticsApplicantCalculator.Rate(completed.Count(x => x.AttemptCount == 1), completed.Count),
			AverageAttempts = activations.Count == 0 ? 0 : Math.Round((decimal)activations.Average(x => x.AttemptCount), 2),
			CommittedIdentifierCount = identifiers.Count(x => x.Status == AdmissionCrmIdentifierStatus.Committed),
			VoidedIdentifierCount = identifiers.Count(x => x.Status == AdmissionCrmIdentifierStatus.Void)
		};
	}

internal static AdmissionCrmFinalAdmissionAnalyticsDto BuildFinalAdmissionAnalytics(
		IReadOnlyCollection<AdmissionCrmSeatPool> seatPools,
		IReadOnlyCollection<AdmissionCrmSeatOffer> offers,
		IReadOnlyCollection<AdmissionCrmSeatReservation> reservations,
		IReadOnlyCollection<AdmissionCrmWaitlistEntry> waitlistEntries,
		IReadOnlyCollection<AdmissionCrmAdmissionDecision> decisions)
	{
		var currentDecisions = decisions
			.GroupBy(x => x.LeadId)
			.Select(x => x
				.OrderByDescending(y => y.DecidedAt ?? y.CreationTime)
				.ThenByDescending(y => y.Id)
				.First())
			.ToList();
		var responseEligibleOffers = offers.Count(x => x.Status is
			AdmissionCrmSeatOfferStatus.Issued or AdmissionCrmSeatOfferStatus.Opened or
			AdmissionCrmSeatOfferStatus.Accepted or
			AdmissionCrmSeatOfferStatus.Declined or
			AdmissionCrmSeatOfferStatus.Expired or
			AdmissionCrmSeatOfferStatus.Cancelled);
		var respondedOffers = offers.Count(x => x.Status is
			AdmissionCrmSeatOfferStatus.Accepted or AdmissionCrmSeatOfferStatus.Declined);
		var acceptedOffers = offers.Count(x => x.Status == AdmissionCrmSeatOfferStatus.Accepted);
		var promotedWaitlistEntries = waitlistEntries.Count(x => x.OfferId.HasValue);
		var decidedApplicants = currentDecisions.Count(x =>
			x.Status != AdmissionCrmAdmissionDecisionStatus.Pending);
		var occupiedSeats = seatPools.Sum(x => x.OccupiedSeats);
		var approvedCapacity = seatPools.Sum(x => x.ApprovedCapacity);

		return new AdmissionCrmFinalAdmissionAnalyticsDto
		{
			SeatPoolCount = seatPools.Count,
			ApprovedCapacity = approvedCapacity,
			ReservedSeatCount = seatPools.Sum(x => x.ReservedSeats),
			AcceptedSeatCount = seatPools.Sum(x => x.AcceptedSeats),
			ActivatedSeatCount = seatPools.Sum(x => x.ActivatedSeats),
			RemainingSeatCount = seatPools.Sum(x => x.RemainingCapacity),
			OverCapacityPoolCount = seatPools.Count(x => x.IsOverCapacity),
			DraftOfferCount = offers.Count(x => x.Status == AdmissionCrmSeatOfferStatus.Draft),
			ApprovalPendingOfferCount = offers.Count(x => x.Status == AdmissionCrmSeatOfferStatus.ApprovalPending),
			IssuedOfferCount = offers.Count(x => x.Status is AdmissionCrmSeatOfferStatus.Issued or AdmissionCrmSeatOfferStatus.Opened),
			AcceptedOfferCount = acceptedOffers,
			DeclinedOfferCount = offers.Count(x => x.Status == AdmissionCrmSeatOfferStatus.Declined),
			ExpiredOfferCount = offers.Count(x => x.Status == AdmissionCrmSeatOfferStatus.Expired),
			CancelledOfferCount = offers.Count(x => x.Status == AdmissionCrmSeatOfferStatus.Cancelled),
			ActiveWaitlistCount = waitlistEntries.Count(x => x.Status == AdmissionCrmWaitlistStatus.Active),
			PromotedWaitlistCount = waitlistEntries.Count(x => x.Status == AdmissionCrmWaitlistStatus.Promoted),
			AcceptedWaitlistCount = waitlistEntries.Count(x => x.Status == AdmissionCrmWaitlistStatus.Accepted),
			CapacityOverrideCount = reservations.Count(x => x.CapacityOverrideUsed),
			ApprovedDecisionCount = currentDecisions.Count(x => x.Status == AdmissionCrmAdmissionDecisionStatus.Approved),
			RejectedDecisionCount = currentDecisions.Count(x => x.Status == AdmissionCrmAdmissionDecisionStatus.Rejected),
			WaitlistedDecisionCount = currentDecisions.Count(x => x.Status == AdmissionCrmAdmissionDecisionStatus.Waitlisted),
			CancelledDecisionCount = currentDecisions.Count(x => x.Status == AdmissionCrmAdmissionDecisionStatus.Cancelled),
			SeatOccupancyRate = AdmissionCrmAnalyticsApplicantCalculator.Rate(occupiedSeats, approvedCapacity),
			OfferResponseRate = AdmissionCrmAnalyticsApplicantCalculator.Rate(respondedOffers, responseEligibleOffers),
			OfferAcceptanceRate = AdmissionCrmAnalyticsApplicantCalculator.Rate(acceptedOffers, responseEligibleOffers),
			WaitlistPromotionRate = AdmissionCrmAnalyticsApplicantCalculator.Rate(promotedWaitlistEntries, waitlistEntries.Count),
			AdmissionApprovalRate = AdmissionCrmAnalyticsApplicantCalculator.Rate(
				currentDecisions.Count(x => x.Status == AdmissionCrmAdmissionDecisionStatus.Approved),
				decidedApplicants)
		};
	}

internal static AdmissionCrmFinancialAnalyticsDto BuildFinancialAnalytics(
		IReadOnlyCollection<AdmissionCrmAdmissionQuotation> quotations,
		IReadOnlyCollection<AdmissionCrmConcessionRequest> concessions,
		DateTime at)
	{
		var issued = quotations
			.Where(x => x.Status == AdmissionCrmQuotationStatus.Issued)
			.ToList();
		var confirmedCount = issued.Count(x => x.FinanceState == AdmissionCrmFinanceState.Confirmed);
		var satisfiedCount = issued.Count(x => x.IsGateSatisfiedAt(at));
		return new AdmissionCrmFinancialAnalyticsDto
		{
			IssuedQuotationCount = issued.Count,
			FinanceConfirmedCount = confirmedCount,
			PendingGateCount = issued.Count - satisfiedCount,
			FailedOrDisputedCount = issued.Count(x => x.FinanceState is
				AdmissionCrmFinanceState.Failed or AdmissionCrmFinanceState.Reversed or AdmissionCrmFinanceState.Disputed),
			GateOverrideCount = issued.Count(x => x.GateOverrideUsed),
			GrossQuotedAmount = issued.Sum(x => x.GrossAmount),
			ConcessionAmount = issued.Sum(x => x.ConcessionAmount),
			NetQuotedAmount = issued.Sum(x => x.NetAmount),
			ConfirmedAmount = issued.Sum(x => x.ConfirmedAmount),
			OutstandingAmount = issued.Sum(x => Math.Max(0, x.NetAmount - x.ConfirmedAmount)),
			ConcessionRequestCount = concessions.Count,
			ConcessionApprovalPendingCount = concessions.Count(x => x.Status == AdmissionCrmConcessionStatus.ApprovalPending),
			FinanceAcknowledgedConcessionCount = concessions.Count(x => x.Status == AdmissionCrmConcessionStatus.FinanceAcknowledged),
			FinanceAcknowledgedConcessionAmount = concessions
				.Where(x => x.Status == AdmissionCrmConcessionStatus.FinanceAcknowledged)
				.Sum(x => x.ApprovedAmount),
			FinanceConfirmationRate = AdmissionCrmAnalyticsApplicantCalculator.Rate(confirmedCount, issued.Count),
			PaymentGateSatisfactionRate = AdmissionCrmAnalyticsApplicantCalculator.Rate(satisfiedCount, issued.Count)
		};
	}

internal static AdmissionCrmHouseholdIdentityAnalyticsDto BuildHouseholdIdentityAnalytics(
		IReadOnlyCollection<AdmissionCrmApplicantIdentityProfile> profiles,
		IReadOnlyCollection<AdmissionCrmGuardianRelationship> guardians,
		IReadOnlyCollection<AdmissionCrmPerson> applicantPeople)
	{
		var reusedHouseholds = profiles.GroupBy(x => x.HouseholdId)
			.Where(x => x.Count() > 1)
			.Select(x => x.Key)
			.ToHashSet();
		var profilesInReusedHouseholds = profiles.Count(x => reusedHouseholds.Contains(x.HouseholdId));
		var readyCount = profiles.Count(x => x.Status is
			AdmissionCrmIdentityResolutionStatus.Ready or AdmissionCrmIdentityResolutionStatus.Overridden);
		return new AdmissionCrmHouseholdIdentityAnalyticsDto
		{
			ProfileCount = profiles.Count,
			ReadyCount = readyCount,
			PendingReviewCount = profiles.Count(x => x.Status is
				AdmissionCrmIdentityResolutionStatus.Unresolved or AdmissionCrmIdentityResolutionStatus.PendingReview),
			ConflictCount = profiles.Count(x => x.Status == AdmissionCrmIdentityResolutionStatus.Conflict),
			OverrideCount = profiles.Count(x => x.OverrideUsed),
			ReusedHouseholdCount = reusedHouseholds.Count,
			GuardianRelationshipCount = guardians.Count,
			MissingAccountableContactCount = profiles.Count(x => !x.AccountableContactReady),
			MissingIdentityConsentCount = profiles.Count(x => !x.IdentityConsentReady),
			AccountLinkPendingCount = profiles.Count(x => !x.AccountLinkReady),
			AadhaarProvidedCount = applicantPeople.Count(x => x.AadhaarStatus != IdentityVerificationStatus.NotProvided),
			PenReadyCount = applicantPeople.Count(x => x.PenStatus == IdentityVerificationStatus.Verified),
			ApaarReadyCount = applicantPeople.Count(x => x.ApaarStatus == IdentityVerificationStatus.Verified),
			IdentityReadinessRate = AdmissionCrmAnalyticsApplicantCalculator.Rate(readyCount, profiles.Count),
			HouseholdReuseRate = AdmissionCrmAnalyticsApplicantCalculator.Rate(profilesInReusedHouseholds, profiles.Count)
		};
	}

internal static AdmissionCrmAutomationAnalyticsDto BuildAutomationAnalytics(
		IReadOnlyCollection<AdmissionCrmAutomationRun> runs,
		int failureCount,
		IReadOnlyCollection<AdmissionCrmTimelineEvent> recentFailures)
	{
		return new AdmissionCrmAutomationAnalyticsDto
		{
			RunCount = runs.Count,
			FailedRunCount = runs.Count(x => x.Status == AdmissionCrmAutomationRunStatus.CompletedWithFailures),
			FailureCount = failureCount,
			RecentRuns = runs
				.OrderByDescending(x => x.CompletedAt)
				.ThenByDescending(x => x.Id)
				.Take(5)
				.Select(x => new AdmissionCrmAutomationRunDto
				{
					Id = x.Id,
					RunType = x.RunType,
					Status = x.Status,
					Trigger = x.Trigger,
					Stage = x.Stage,
					LeadId = x.LeadId,
					StartedAt = x.StartedAt,
					CompletedAt = x.CompletedAt,
					EvaluatedLeadCount = x.EvaluatedLeadCount,
					MatchedRuleCount = x.MatchedRuleCount,
					CreatedTaskCount = x.CreatedTaskCount,
					SentMessageCount = x.SentMessageCount,
					SkippedExistingTaskCount = x.SkippedExistingTaskCount,
					SkippedExistingMessageCount = x.SkippedExistingMessageCount,
					FailedActionCount = x.FailedActionCount,
					MarkedStaleCount = x.MarkedStaleCount,
					ArchivedCount = x.ArchivedCount,
					Details = x.Details
				})
				.ToList(),
			RecentFailures = recentFailures
				.Select(x => new AdmissionCrmAutomationFailureDto
				{
					EventId = x.Id,
					LeadId = x.LeadId,
					Title = x.Title,
					Details = x.Details,
					OccurredAt = x.OccurredAt
				})
				.ToList()
		};
	}
}

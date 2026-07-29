using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public sealed record AdmissionCrmApplicantProgressRules(
	bool EntranceExamRequired,
	bool FinanceVerificationRequired,
	AdmissionCrmPlacementTiming PlacementTiming);

public class AdmissionCrmApplicantProgressProjector : ITransientDependency
{
	public AdmissionCrmApplicantProgressDto Project(
		AdmissionInquiryLead lead,
		AdmissionCrmApplicantProgressRules rules,
		AdmissionCrmEntranceExamRecord? exam,
		AdmissionCrmRegistrationApplication? registration,
		AdmissionCrmAdmissionQuotation? quotation,
		AdmissionCrmSeatOffer? seatOffer,
		AdmissionCrmActivation? activation,
		bool hasOpenFollowUp)
	{
		var inquiry = Step(
			AdmissionCrmApplicantStepKey.Inquiry,
			"Inquiry",
			AdmissionCrmApplicantStepStatus.Complete,
			true,
			"InquiryCaptured",
			"Inquiry details were captured in Pipeline.",
			completedAt: lead.CreationTime);
		var examStep = BuildExamStep(rules.EntranceExamRequired, exam);
		var registrationStep = BuildRegistrationStep(examStep, registration);
		var financeStep = BuildFinanceStep(
			rules.FinanceVerificationRequired,
			registrationStep,
			quotation);
		var placementStep = BuildPlacementStep(
			rules.PlacementTiming,
			registrationStep,
			financeStep,
			seatOffer,
			activation);
		var activationStep = BuildActivationStep(
			registrationStep,
			examStep,
			financeStep,
			placementStep,
			lead,
			activation);
		var steps = new[]
		{
			inquiry,
			examStep,
			registrationStep,
			financeStep,
			placementStep,
			activationStep
		};
		var next = steps.FirstOrDefault(step =>
			step.Status is not (
				AdmissionCrmApplicantStepStatus.Complete or
				AdmissionCrmApplicantStepStatus.Skipped)) ?? activationStep;

		return new AdmissionCrmApplicantProgressDto
		{
			LeadId = lead.Id,
			AcademicYearId = lead.AcademicYearId,
			AdmissionYearSetupId = lead.AdmissionYearSetupId,
			AdmissionYearSetupVersion = lead.AdmissionYearSetupVersion ?? 0,
			PipelineColumn = ResolvePipelineColumn(
				lead,
				exam,
				registration,
				registrationStep,
				financeStep,
				placementStep,
				activationStep,
				seatOffer,
				hasOpenFollowUp),
			ClosedOutcome = ResolveClosedOutcome(lead, exam, registration, activation),
			CurrentStep = next.Step,
			NextAction = next.Reason,
			NextActionCode = next.ActionCode,
			Steps = steps
		};
	}

	private static AdmissionCrmApplicantStepDto BuildExamStep(
		bool required,
		AdmissionCrmEntranceExamRecord? exam)
	{
		if (!required)
		{
			return Step(
				AdmissionCrmApplicantStepKey.Exam,
				"Entrance Exam",
				AdmissionCrmApplicantStepStatus.Skipped,
				false,
				"ExamNotRequired",
				"Entrance exam is not required by the applied Admission Year Setup.");
		}

		if (exam is null)
		{
			return Step(
				AdmissionCrmApplicantStepKey.Exam,
				"Entrance Exam",
				AdmissionCrmApplicantStepStatus.NotStarted,
				true,
				"ExamNotScheduled",
				"Schedule the entrance exam or send the scheduling invitation.",
				"ScheduleExam");
		}

		if (exam.AttendanceStatus == AdmissionCrmExamAttendanceStatus.Absent)
		{
			return Step(
				AdmissionCrmApplicantStepKey.Exam,
				"Entrance Exam",
				AdmissionCrmApplicantStepStatus.NeedsReview,
				true,
				"ExamAbsent",
				"Applicant was absent. Decide whether a retest is allowed.",
				"ReviewExamAbsence");
		}

		return exam.EligibilityDecision switch
		{
			AdmissionCrmEligibilityDecision.Eligible => Step(
				AdmissionCrmApplicantStepKey.Exam,
				"Entrance Exam",
				AdmissionCrmApplicantStepStatus.Complete,
				true,
				"ExamEligible",
				"Entrance exam eligibility is complete.",
				completedAt: exam.EligibilityDecidedAt ?? exam.ResultRecordedAt),
			AdmissionCrmEligibilityDecision.Waitlisted => Step(
				AdmissionCrmApplicantStepKey.Exam,
				"Entrance Exam",
				AdmissionCrmApplicantStepStatus.NeedsReview,
				true,
				"ExamWaitlisted",
				"Eligibility is waitlisted and requires an admission decision.",
				"ReviewEligibility"),
			AdmissionCrmEligibilityDecision.Rejected => Step(
				AdmissionCrmApplicantStepKey.Exam,
				"Entrance Exam",
				AdmissionCrmApplicantStepStatus.Blocked,
				true,
				"ExamIneligible",
				"Applicant is ineligible under the recorded exam decision."),
			_ when exam.ResultRecordedAt.HasValue => Step(
				AdmissionCrmApplicantStepKey.Exam,
				"Entrance Exam",
				AdmissionCrmApplicantStepStatus.NeedsReview,
				true,
				"EligibilityPending",
				"Exam result is recorded and eligibility needs review.",
				"ReviewEligibility"),
			_ => Step(
				AdmissionCrmApplicantStepKey.Exam,
				"Entrance Exam",
				AdmissionCrmApplicantStepStatus.InProgress,
				true,
				"ExamPending",
				"Record attendance and the entrance exam result.",
				"RecordExamResult")
		};
	}

	private static AdmissionCrmApplicantStepDto BuildRegistrationStep(
		AdmissionCrmApplicantStepDto exam,
		AdmissionCrmRegistrationApplication? registration)
	{
		if (exam.Status is AdmissionCrmApplicantStepStatus.Blocked or AdmissionCrmApplicantStepStatus.NeedsReview)
		{
			return Step(
				AdmissionCrmApplicantStepKey.Registration,
				"Registration",
				AdmissionCrmApplicantStepStatus.Blocked,
				true,
				"ExamDecisionRequired",
				"Complete the entrance exam decision before registration.");
		}

		if (exam.Status is AdmissionCrmApplicantStepStatus.NotStarted or AdmissionCrmApplicantStepStatus.InProgress)
		{
			return Step(
				AdmissionCrmApplicantStepKey.Registration,
				"Registration",
				AdmissionCrmApplicantStepStatus.Blocked,
				true,
				"ExamIncomplete",
				"Complete the required entrance exam before registration.");
		}

		if (registration is null)
		{
			return Step(
				AdmissionCrmApplicantStepKey.Registration,
				"Registration",
				AdmissionCrmApplicantStepStatus.NotStarted,
				true,
				"RegistrationNotIssued",
				"Issue or start the registration form.",
				"StartRegistration");
		}

		return registration.Status switch
		{
			AdmissionCrmRegistrationStatus.Verified or AdmissionCrmRegistrationStatus.AdmissionReady =>
				Step(
					AdmissionCrmApplicantStepKey.Registration,
					"Registration",
					AdmissionCrmApplicantStepStatus.Complete,
					true,
					"RegistrationVerified",
					"Registration details and documents are verified.",
					completedAt: registration.VerifiedAt),
			AdmissionCrmRegistrationStatus.Submitted or AdmissionCrmRegistrationStatus.Resubmitted =>
				Step(
					AdmissionCrmApplicantStepKey.Registration,
					"Registration",
					AdmissionCrmApplicantStepStatus.NeedsReview,
					true,
					"RegistrationReviewRequired",
					"Review and verify the submitted registration.",
					"VerifyRegistration"),
			AdmissionCrmRegistrationStatus.ReturnedForCorrection =>
				Step(
					AdmissionCrmApplicantStepKey.Registration,
					"Registration",
					AdmissionCrmApplicantStepStatus.Blocked,
					true,
					"RegistrationCorrectionRequired",
					"Registration was returned for correction.",
					"ReviewRegistrationCorrection"),
			AdmissionCrmRegistrationStatus.Expired or AdmissionCrmRegistrationStatus.Revoked =>
				Step(
					AdmissionCrmApplicantStepKey.Registration,
					"Registration",
					AdmissionCrmApplicantStepStatus.Blocked,
					true,
					"RegistrationLinkUnavailable",
					"Registration link must be reissued before work can continue.",
					"ReissueRegistration"),
			AdmissionCrmRegistrationStatus.Withdrawn =>
				Step(
					AdmissionCrmApplicantStepKey.Registration,
					"Registration",
					AdmissionCrmApplicantStepStatus.Blocked,
					true,
					"RegistrationWithdrawn",
					"Registration was withdrawn."),
			_ => Step(
				AdmissionCrmApplicantStepKey.Registration,
				"Registration",
				AdmissionCrmApplicantStepStatus.InProgress,
				true,
				"RegistrationInProgress",
				"Complete and submit the registration form.",
				"ContinueRegistration")
		};
	}

	private static AdmissionCrmApplicantStepDto BuildFinanceStep(
		bool required,
		AdmissionCrmApplicantStepDto registration,
		AdmissionCrmAdmissionQuotation? quotation)
	{
		if (!required)
		{
			return Step(
				AdmissionCrmApplicantStepKey.Finance,
				"Finance",
				AdmissionCrmApplicantStepStatus.Skipped,
				false,
				"FinanceNotRequired",
				"Finance verification is not required by the applied Admission Year Setup.");
		}

		if (registration.Status != AdmissionCrmApplicantStepStatus.Complete)
		{
			return Step(
				AdmissionCrmApplicantStepKey.Finance,
				"Finance",
				AdmissionCrmApplicantStepStatus.Blocked,
				true,
				"RegistrationNotVerified",
				"Verify registration before requesting Finance review.");
		}

		if (quotation is null)
		{
			return Step(
				AdmissionCrmApplicantStepKey.Finance,
				"Finance",
				AdmissionCrmApplicantStepStatus.NotStarted,
				true,
				"FinanceNotRequested",
				"Request admission-fee verification from Finance.",
				"RequestFinanceVerification");
		}

		if (quotation.FinanceState is
			AdmissionCrmFinanceState.Confirmed or
			AdmissionCrmFinanceState.Waived or
			AdmissionCrmFinanceState.Sponsored)
		{
			return Step(
				AdmissionCrmApplicantStepKey.Finance,
				"Finance",
				AdmissionCrmApplicantStepStatus.Complete,
				true,
				"FinanceVerified",
				"Finance confirmed payment or an approved waiver.",
				completedAt: quotation.FinanceConfirmedAt);
		}

		if (quotation.FinanceState is
			AdmissionCrmFinanceState.Failed or
			AdmissionCrmFinanceState.Reversed or
			AdmissionCrmFinanceState.Disputed)
		{
			return Step(
				AdmissionCrmApplicantStepKey.Finance,
				"Finance",
				AdmissionCrmApplicantStepStatus.Blocked,
				true,
				"FinanceBlocked",
				"Finance reported an issue that must be resolved.",
				"ReviewFinanceIssue");
		}

		return Step(
			AdmissionCrmApplicantStepKey.Finance,
			"Finance",
			AdmissionCrmApplicantStepStatus.InProgress,
			true,
			"FinancePending",
			"Finance verification is pending.",
			"ViewFinanceReference");
	}

	private static AdmissionCrmApplicantStepDto BuildPlacementStep(
		AdmissionCrmPlacementTiming timing,
		AdmissionCrmApplicantStepDto registration,
		AdmissionCrmApplicantStepDto finance,
		AdmissionCrmSeatOffer? seatOffer,
		AdmissionCrmActivation? activation)
	{
		if (registration.Status != AdmissionCrmApplicantStepStatus.Complete ||
			finance.Status is not (
				AdmissionCrmApplicantStepStatus.Complete or
				AdmissionCrmApplicantStepStatus.Skipped))
		{
			return Step(
				AdmissionCrmApplicantStepKey.Placement,
				"Class Placement",
				AdmissionCrmApplicantStepStatus.Blocked,
				true,
				"PriorGateIncomplete",
				"Complete registration and required Finance verification before placement.");
		}

		var activationHasPlacement = activation is not null &&
			activation.StandardId != Guid.Empty &&
			(timing == AdmissionCrmPlacementTiming.StandardBeforeActivation ||
			 activation.DivisionId != Guid.Empty);
		if (activationHasPlacement || seatOffer?.Status == AdmissionCrmSeatOfferStatus.Accepted)
		{
			return Step(
				AdmissionCrmApplicantStepKey.Placement,
				"Class Placement",
				AdmissionCrmApplicantStepStatus.Complete,
				true,
				"PlacementConfirmed",
				timing == AdmissionCrmPlacementTiming.DivisionBeforeActivation
					? "Standard and division placement are confirmed."
					: "Board, medium, and standard placement are confirmed.",
				completedAt: seatOffer?.RespondedAt ?? activation?.RequestedAt);
		}

		if (seatOffer is not null)
		{
			return Step(
				AdmissionCrmApplicantStepKey.Placement,
				"Class Placement",
				AdmissionCrmApplicantStepStatus.InProgress,
				true,
				"PlacementInProgress",
				"Complete the pending class placement review.",
				"ReviewPlacement");
		}

		return Step(
			AdmissionCrmApplicantStepKey.Placement,
			"Class Placement",
			AdmissionCrmApplicantStepStatus.NotStarted,
			true,
			"PlacementNotReviewed",
			timing == AdmissionCrmPlacementTiming.DivisionBeforeActivation
				? "Confirm board, medium, standard, division, and capacity."
				: "Confirm board, medium, standard, and capacity.",
			"ReviewPlacement");
	}

	private static AdmissionCrmApplicantStepDto BuildActivationStep(
		AdmissionCrmApplicantStepDto registration,
		AdmissionCrmApplicantStepDto exam,
		AdmissionCrmApplicantStepDto finance,
		AdmissionCrmApplicantStepDto placement,
		AdmissionInquiryLead lead,
		AdmissionCrmActivation? activation)
	{
		if (lead.StudentId.HasValue ||
			activation?.Status == AdmissionCrmActivationStatus.FirstDayReady)
		{
			return Step(
				AdmissionCrmApplicantStepKey.Activation,
				"Activation",
				AdmissionCrmApplicantStepStatus.Complete,
				true,
				"StudentActivated",
				"Student record is active and owned by Student Records.",
				completedAt: activation?.FirstDayReadyAt);
		}

		if (activation?.Status == AdmissionCrmActivationStatus.RepairRequired)
		{
			return Step(
				AdmissionCrmApplicantStepKey.Activation,
				"Activation",
				AdmissionCrmApplicantStepStatus.Blocked,
				true,
				"ActivationRepairRequired",
				activation.RepairReason ?? "Activation requires repair.",
				"RepairActivation");
		}

		if (activation?.Status is
			AdmissionCrmActivationStatus.ApprovalPending or
			AdmissionCrmActivationStatus.CancellationPending)
		{
			return Step(
				AdmissionCrmApplicantStepKey.Activation,
				"Activation",
				AdmissionCrmApplicantStepStatus.NeedsReview,
				true,
				"ActivationApprovalRequired",
				"An authorized approver must review activation.",
				"ReviewActivation");
		}

		if (activation?.Status is
			AdmissionCrmActivationStatus.Approved or
			AdmissionCrmActivationStatus.InProgress)
		{
			return Step(
				AdmissionCrmApplicantStepKey.Activation,
				"Activation",
				AdmissionCrmApplicantStepStatus.InProgress,
				true,
				"ActivationInProgress",
				"Complete student activation and verify the handoff.",
				"ContinueActivation");
		}

		var gatesReady =
			registration.Status == AdmissionCrmApplicantStepStatus.Complete &&
			exam.Status is AdmissionCrmApplicantStepStatus.Complete or AdmissionCrmApplicantStepStatus.Skipped &&
			finance.Status is AdmissionCrmApplicantStepStatus.Complete or AdmissionCrmApplicantStepStatus.Skipped &&
			placement.Status == AdmissionCrmApplicantStepStatus.Complete;
		return gatesReady
			? Step(
				AdmissionCrmApplicantStepKey.Activation,
				"Activation",
				AdmissionCrmApplicantStepStatus.NotStarted,
				true,
				"ReadyForActivation",
				"All required gates are complete. Request activation approval.",
				"RequestActivation")
			: Step(
				AdmissionCrmApplicantStepKey.Activation,
				"Activation",
				AdmissionCrmApplicantStepStatus.Blocked,
				true,
				"ActivationGatesIncomplete",
				"Complete all required admission gates before activation.");
	}

	private static AdmissionCrmPipelineColumnKey ResolvePipelineColumn(
		AdmissionInquiryLead lead,
		AdmissionCrmEntranceExamRecord? exam,
		AdmissionCrmRegistrationApplication? registration,
		AdmissionCrmApplicantStepDto registrationStep,
		AdmissionCrmApplicantStepDto finance,
		AdmissionCrmApplicantStepDto placement,
		AdmissionCrmApplicantStepDto activation,
		AdmissionCrmSeatOffer? seatOffer,
		bool hasOpenFollowUp)
	{
		if (activation.Status == AdmissionCrmApplicantStepStatus.Complete)
			return AdmissionCrmPipelineColumnKey.Activated;
		if (activation.Status is
			AdmissionCrmApplicantStepStatus.NotStarted or
			AdmissionCrmApplicantStepStatus.InProgress or
			AdmissionCrmApplicantStepStatus.NeedsReview)
			return AdmissionCrmPipelineColumnKey.ReadyForActivation;
		if (registration is null &&
			exam?.EligibilityDecision == AdmissionCrmEligibilityDecision.Eligible)
			return AdmissionCrmPipelineColumnKey.Eligible;
		if (registrationStep.Status == AdmissionCrmApplicantStepStatus.Complete)
		{
			if (finance.Status is not (
				AdmissionCrmApplicantStepStatus.Complete or
				AdmissionCrmApplicantStepStatus.Skipped))
				return AdmissionCrmPipelineColumnKey.FinanceVerification;
			if (finance.Status == AdmissionCrmApplicantStepStatus.Skipped &&
				seatOffer is null &&
				placement.ReasonCode == "PlacementNotReviewed")
				return AdmissionCrmPipelineColumnKey.RegistrationDone;
			if (placement.Status is not AdmissionCrmApplicantStepStatus.Complete)
				return AdmissionCrmPipelineColumnKey.PlacementPending;
			return AdmissionCrmPipelineColumnKey.ReadyForActivation;
		}
		if (registration is not null &&
			registrationStep.Status is
			AdmissionCrmApplicantStepStatus.NotStarted or
			AdmissionCrmApplicantStepStatus.InProgress or
			AdmissionCrmApplicantStepStatus.NeedsReview)
			return AdmissionCrmPipelineColumnKey.RegistrationPending;
		if (exam?.ResultRecordedAt.HasValue == true ||
			exam?.EligibilityDecision is
				AdmissionCrmEligibilityDecision.Waitlisted or
				AdmissionCrmEligibilityDecision.Rejected)
			return AdmissionCrmPipelineColumnKey.ExamDone;
		if (exam is not null || lead.Stage is
			AdmissionInquiryStage.ExamReady or
			AdmissionInquiryStage.ExamScheduled)
			return AdmissionCrmPipelineColumnKey.ExamPending;
		if (hasOpenFollowUp || lead.Stage == AdmissionInquiryStage.FollowUp)
			return AdmissionCrmPipelineColumnKey.FollowUp;
		if (registrationStep.Status == AdmissionCrmApplicantStepStatus.NotStarted &&
			exam is null &&
			lead.Stage is not AdmissionInquiryStage.NewInquiry)
			return AdmissionCrmPipelineColumnKey.RegistrationPending;
		return AdmissionCrmPipelineColumnKey.NewInquiry;
	}

	private static AdmissionCrmClosedOutcome? ResolveClosedOutcome(
		AdmissionInquiryLead lead,
		AdmissionCrmEntranceExamRecord? exam,
		AdmissionCrmRegistrationApplication? registration,
		AdmissionCrmActivation? activation)
	{
		if (lead.MergedIntoLeadId.HasValue)
			return AdmissionCrmClosedOutcome.Duplicate;
		if (activation?.Status == AdmissionCrmActivationStatus.Cancelled)
			return AdmissionCrmClosedOutcome.Cancelled;
		if (exam?.EligibilityDecision == AdmissionCrmEligibilityDecision.Rejected)
			return AdmissionCrmClosedOutcome.Ineligible;
		if (registration?.Status == AdmissionCrmRegistrationStatus.Withdrawn)
			return AdmissionCrmClosedOutcome.Withdrawn;
		if (lead.Stage == AdmissionInquiryStage.Rejected)
			return AdmissionCrmClosedOutcome.Rejected;
		if (lead.Stage == AdmissionInquiryStage.Archived)
			return AdmissionCrmClosedOutcome.Withdrawn;
		if (lead.Stage == AdmissionInquiryStage.Stale)
			return AdmissionCrmClosedOutcome.NotInterested;
		return null;
	}

	private static AdmissionCrmApplicantStepDto Step(
		AdmissionCrmApplicantStepKey key,
		string title,
		AdmissionCrmApplicantStepStatus status,
		bool required,
		string reasonCode,
		string reason,
		string? actionCode = null,
		DateTime? completedAt = null) =>
		new()
		{
			Step = key,
			Title = title,
			Status = status,
			IsRequired = required,
			ReasonCode = reasonCode,
			Reason = reason,
			ActionCode = actionCode,
			CompletedAt = completedAt
		};
}

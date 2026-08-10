using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal sealed record AdmissionCrmAnalyticsCoreSnapshot(
	IReadOnlyList<AdmissionInquiryLead> DimensionLeads,
	IReadOnlyList<AdmissionInquiryLead> Leads,
	IReadOnlyList<AdmissionCrmEntranceExamRecord> DimensionExams,
	IReadOnlyList<AdmissionCrmEntranceExamRecord> Exams,
	IReadOnlyList<AdmissionCrmFollowUpTask> DimensionTasks,
	IReadOnlyList<AdmissionCrmFollowUpTask> Tasks,
	IReadOnlyList<AdmissionCrmPipelineStage> Stages,
	IReadOnlyList<AdmissionCrmTimelineEvent> DimensionTimelineEvents,
	IReadOnlyList<AdmissionCrmTimelineEvent> SummaryTimelineEvents,
	IReadOnlyList<AdmissionCrmAutomationRun> AutomationRuns,
	AdmissionCrmCycleConfiguration? CycleConfiguration);

internal sealed record AdmissionCrmAnalyticsApplicantSnapshot(
	IReadOnlyList<AdmissionCrmRegistrationApplication> Registrations,
	IReadOnlyList<AdmissionCrmDocumentChecklistAssignment> Checklists,
	IReadOnlyList<AdmissionCrmApplicantDocument> Documents,
	IReadOnlyList<AdmissionCrmExamSlot> Slots);

internal sealed record AdmissionCrmAnalyticsDecisionSnapshot(
	IReadOnlyList<AdmissionCrmSeatPool> SeatPools,
	IReadOnlyList<AdmissionCrmSeatOffer> SeatOffers,
	IReadOnlyList<AdmissionCrmSeatReservation> SeatReservations,
	IReadOnlyList<AdmissionCrmWaitlistEntry> WaitlistEntries,
	IReadOnlyList<AdmissionCrmAdmissionDecision> AdmissionDecisions,
	IReadOnlyList<AdmissionCrmAdmissionQuotation> Quotations,
	IReadOnlyList<AdmissionCrmConcessionRequest> Concessions);

internal sealed record AdmissionCrmAnalyticsIdentitySnapshot(
	IReadOnlyList<AdmissionCrmApplicantIdentityProfile> IdentityProfiles,
	IReadOnlyList<AdmissionCrmGuardianRelationship> GuardianRelationships,
	IReadOnlyList<AdmissionCrmPerson> ApplicantPeople);

internal sealed record AdmissionCrmAnalyticsGovernanceSnapshot(
	IReadOnlyList<AdmissionCrmPolicyPack> PolicyPacks,
	IReadOnlyList<AdmissionCrmPolicyAdoption> PolicyAdoptions,
	IReadOnlyList<AdmissionCrmPolicyEvaluation> PolicyEvaluations,
	IReadOnlyList<AdmissionCrmPolicyException> PolicyExceptions,
	IReadOnlyList<AdmissionCrmRouteAssignment> RouteAssignments,
	IReadOnlyList<AdmissionCrmRouteAppeal> RouteAppeals,
	IReadOnlyList<AdmissionCrmActivation> Activations,
	IReadOnlyList<AdmissionCrmIdentifierAllocation> Identifiers);

using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmAnalyticsSummaryDto
{
	public Guid AcademicYearId { get; set; }
	public DateTime TrendFromDate { get; set; }
	public DateTime TrendToDate { get; set; }
	public int TotalLeadCount { get; set; }
	public int ActiveLeadCount { get; set; }
	public int ExamScheduledLeadCount { get; set; }
	public int EligibilityHandoffCount { get; set; }
	public int StaleLeadCount { get; set; }
	public int ArchivedLeadCount { get; set; }
	public int StaleOrArchivedLeadCount { get; set; }
	public int DuplicateWarningCount { get; set; }
	public decimal ExamScheduledRate { get; set; }
	public decimal EligibilityHandoffRate { get; set; }
	public decimal StaleRate { get; set; }
	public decimal ArchiveRate { get; set; }
	public decimal StaleOrArchivedRate { get; set; }
	public decimal DuplicateWarningRate { get; set; }
	public decimal AverageLeadScore { get; set; }
	public IReadOnlyList<AdmissionCrmStageFunnelDto> StageFunnel { get; set; } = [];
	public IReadOnlyList<AdmissionCrmChannelPerformanceDto> ChannelPerformance { get; set; } = [];
	public IReadOnlyList<AdmissionCrmSourcePerformanceDto> SourcePerformance { get; set; } = [];
	public IReadOnlyList<AdmissionCrmCampaignPerformanceDto> CampaignPerformance { get; set; } = [];
	public IReadOnlyList<AdmissionCrmStaffWorkloadDto> StaffWorkload { get; set; } = [];
	public AdmissionCrmTaskAnalyticsDto TaskAnalytics { get; set; } = new();
	public AdmissionCrmSlaAnalyticsDto SlaAnalytics { get; set; } = new();
	public AdmissionCrmExamAnalyticsDto ExamAnalytics { get; set; } = new();
	public AdmissionCrmRegistrationAnalyticsDto RegistrationAnalytics { get; set; } = new();
	public AdmissionCrmDocumentAnalyticsDto DocumentAnalytics { get; set; } = new();
	public AdmissionCrmFinalAdmissionAnalyticsDto FinalAdmissionAnalytics { get; set; } = new();
	public AdmissionCrmFinancialAnalyticsDto FinancialAnalytics { get; set; } = new();
	public AdmissionCrmHouseholdIdentityAnalyticsDto HouseholdIdentityAnalytics { get; set; } = new();
	public AdmissionCrmPolicyAnalyticsDto PolicyAnalytics { get; set; } = new();
	public AdmissionCrmRouteAnalyticsDto RouteAnalytics { get; set; } = new();
	public AdmissionCrmActivationAnalyticsDto ActivationAnalytics { get; set; } = new();
	public AdmissionCrmAutomationAnalyticsDto AutomationAnalytics { get; set; } = new();
	public IReadOnlyList<AdmissionCrmDailyTrendDto> DailyTrends { get; set; } = [];
}

public class AdmissionCrmPolicyAnalyticsDto
{
	public int ActiveAdoptionCount { get; set; }
	public int EvaluationCount { get; set; }
	public int BlockingFailureCount { get; set; }
	public int EvidencePendingCount { get; set; }
	public int ApprovedExceptionCount { get; set; }
	public int SourceReviewOverdueCount { get; set; }
}

public class AdmissionCrmRouteAnalyticsDto
{
	public int AssignedApplicantCount { get; set; }
	public int EligibleCount { get; set; }
	public int PendingReviewCount { get; set; }
	public int EvidencePendingCount { get; set; }
	public int RouteChangeCount { get; set; }
	public int AppealPendingCount { get; set; }
	public int AppealApprovedCount { get; set; }
	public IReadOnlyDictionary<string, int> ApplicantsByRoute { get; set; } = new Dictionary<string, int>();
}

public class AdmissionCrmActivationAnalyticsDto
{
	public int RequestedCount { get; set; }
	public int ApprovalPendingCount { get; set; }
	public int FirstDayReadyCount { get; set; }
	public int RepairRequiredCount { get; set; }
	public int CancellationPendingCount { get; set; }
	public int CancelledCount { get; set; }
	public int FirstPassSuccessCount { get; set; }
	public decimal FirstPassSuccessRate { get; set; }
	public decimal AverageAttempts { get; set; }
	public int CommittedIdentifierCount { get; set; }
	public int VoidedIdentifierCount { get; set; }
}

public class AdmissionCrmHouseholdIdentityAnalyticsDto
{
	public int ProfileCount { get; set; }
	public int ReadyCount { get; set; }
	public int PendingReviewCount { get; set; }
	public int ConflictCount { get; set; }
	public int OverrideCount { get; set; }
	public int ReusedHouseholdCount { get; set; }
	public int GuardianRelationshipCount { get; set; }
	public int MissingAccountableContactCount { get; set; }
	public int MissingIdentityConsentCount { get; set; }
	public int AccountLinkPendingCount { get; set; }
	public int AadhaarProvidedCount { get; set; }
	public int PenReadyCount { get; set; }
	public int ApaarReadyCount { get; set; }
	public decimal IdentityReadinessRate { get; set; }
	public decimal HouseholdReuseRate { get; set; }
}

public class AdmissionCrmFinancialAnalyticsDto
{
	public int IssuedQuotationCount { get; set; }
	public int FinanceConfirmedCount { get; set; }
	public int PendingGateCount { get; set; }
	public int FailedOrDisputedCount { get; set; }
	public int GateOverrideCount { get; set; }
	public decimal GrossQuotedAmount { get; set; }
	public decimal ConcessionAmount { get; set; }
	public decimal NetQuotedAmount { get; set; }
	public decimal ConfirmedAmount { get; set; }
	public decimal OutstandingAmount { get; set; }
	public int ConcessionRequestCount { get; set; }
	public int ConcessionApprovalPendingCount { get; set; }
	public int FinanceAcknowledgedConcessionCount { get; set; }
	public decimal FinanceAcknowledgedConcessionAmount { get; set; }
	public decimal FinanceConfirmationRate { get; set; }
	public decimal PaymentGateSatisfactionRate { get; set; }
}

public class AdmissionCrmFinalAdmissionAnalyticsDto
{
	public int SeatPoolCount { get; set; }
	public int ApprovedCapacity { get; set; }
	public int ReservedSeatCount { get; set; }
	public int AcceptedSeatCount { get; set; }
	public int ActivatedSeatCount { get; set; }
	public int RemainingSeatCount { get; set; }
	public int OverCapacityPoolCount { get; set; }
	public int DraftOfferCount { get; set; }
	public int ApprovalPendingOfferCount { get; set; }
	public int IssuedOfferCount { get; set; }
	public int AcceptedOfferCount { get; set; }
	public int DeclinedOfferCount { get; set; }
	public int ExpiredOfferCount { get; set; }
	public int CancelledOfferCount { get; set; }
	public int ActiveWaitlistCount { get; set; }
	public int PromotedWaitlistCount { get; set; }
	public int AcceptedWaitlistCount { get; set; }
	public int CapacityOverrideCount { get; set; }
	public int ApprovedDecisionCount { get; set; }
	public int RejectedDecisionCount { get; set; }
	public int WaitlistedDecisionCount { get; set; }
	public int CancelledDecisionCount { get; set; }
	public decimal SeatOccupancyRate { get; set; }
	public decimal OfferResponseRate { get; set; }
	public decimal OfferAcceptanceRate { get; set; }
	public decimal WaitlistPromotionRate { get; set; }
	public decimal AdmissionApprovalRate { get; set; }
}

public class AdmissionCrmSlaAnalyticsDto
{
	public int FirstResponseSlaHours { get; set; }
	public int FirstResponseCount { get; set; }
	public int FirstResponsePendingCount { get; set; }
	public int FirstResponseSlaMetCount { get; set; }
	public int FirstResponseSlaMissedCount { get; set; }
	public decimal FirstResponseRate { get; set; }
	public decimal FirstResponseSlaMetRate { get; set; }
	public decimal FirstResponseSlaMissedRate { get; set; }
	public decimal AverageFirstResponseHours { get; set; }
	public int FollowUpSlaMissedCount { get; set; }
	public decimal FollowUpSlaMissedRate { get; set; }
}

public class AdmissionCrmAutomationAnalyticsDto
{
	public int RunCount { get; set; }
	public int FailedRunCount { get; set; }
	public int FailureCount { get; set; }
	public IReadOnlyList<AdmissionCrmAutomationRunDto> RecentRuns { get; set; } = [];
	public IReadOnlyList<AdmissionCrmAutomationFailureDto> RecentFailures { get; set; } = [];
}

public class AdmissionCrmAutomationRunDto
{
	public Guid Id { get; set; }
	public AdmissionCrmAutomationRunType RunType { get; set; }
	public AdmissionCrmAutomationRunStatus Status { get; set; }
	public AdmissionCrmJourneyTrigger? Trigger { get; set; }
	public AdmissionInquiryStage? Stage { get; set; }
	public Guid? LeadId { get; set; }
	public DateTimeOffset StartedAt { get; set; }
	public DateTimeOffset CompletedAt { get; set; }
	public int EvaluatedLeadCount { get; set; }
	public int MatchedRuleCount { get; set; }
	public int CreatedTaskCount { get; set; }
	public int SentMessageCount { get; set; }
	public int SkippedExistingTaskCount { get; set; }
	public int SkippedExistingMessageCount { get; set; }
	public int FailedActionCount { get; set; }
	public int MarkedStaleCount { get; set; }
	public int ArchivedCount { get; set; }
	public string? Details { get; set; }
}

public class AdmissionCrmAutomationFailureDto
{
	public Guid EventId { get; set; }
	public Guid LeadId { get; set; }
	public string Title { get; set; } = string.Empty;
	public string? Details { get; set; }
	public DateTimeOffset OccurredAt { get; set; }
}

public class AdmissionCrmDailyTrendDto
{
	public DateTime Date { get; set; }
	public int LeadCount { get; set; }
	public int ExamScheduledCount { get; set; }
	public int ExamCompletedCount { get; set; }
	public int EligibleCount { get; set; }
	public int WaitlistedCount { get; set; }
	public int RejectedCount { get; set; }
	public int StaleCount { get; set; }
	public int ArchivedCount { get; set; }
	public int SlaMissedCount { get; set; }
}

public class AdmissionCrmStageFunnelDto
{
	public AdmissionInquiryStage Stage { get; set; }
	public string StageName { get; set; } = string.Empty;
	public int LeadCount { get; set; }
	public decimal ShareOfLeads { get; set; }
}

public class AdmissionCrmChannelPerformanceDto
{
	public AdmissionInquiryChannel Channel { get; set; }
	public string ChannelName { get; set; } = string.Empty;
	public int LeadCount { get; set; }
	public int ExamScheduledCount { get; set; }
	public int EligibleCount { get; set; }
	public int WaitlistedCount { get; set; }
	public int RejectedCount { get; set; }
	public decimal EligibilityRate { get; set; }
}

public class AdmissionCrmSourcePerformanceDto
{
	public string SourceName { get; set; } = string.Empty;
	public int LeadCount { get; set; }
	public int ExamScheduledCount { get; set; }
	public int EligibleCount { get; set; }
	public decimal EligibilityRate { get; set; }
}

public class AdmissionCrmCampaignPerformanceDto
{
	public string CampaignName { get; set; } = string.Empty;
	public int LeadCount { get; set; }
	public int ExamScheduledCount { get; set; }
	public int EligibleCount { get; set; }
	public int WaitlistedCount { get; set; }
	public int RejectedCount { get; set; }
	public decimal EligibilityRate { get; set; }
	public decimal AverageLeadScore { get; set; }
}

public class AdmissionCrmTaskAnalyticsDto
{
	public int PendingCount { get; set; }
	public int CompletedCount { get; set; }
	public int OverdueCount { get; set; }
}

public class AdmissionCrmStaffWorkloadDto
{
	public Guid? OwnerUserId { get; set; }
	public string OwnerLabel { get; set; } = string.Empty;
	public int LeadCount { get; set; }
	public int OpenLeadCount { get; set; }
	public int StaleLeadCount { get; set; }
	public int PendingTaskCount { get; set; }
	public int OverdueTaskCount { get; set; }
	public int ExamScheduledCount { get; set; }
}

public class AdmissionCrmExamAnalyticsDto
{
	public int ScheduledCount { get; set; }
	public int PresentCount { get; set; }
	public int AbsentCount { get; set; }
	public int NotMarkedCount { get; set; }
	public int PassCount { get; set; }
	public int FailCount { get; set; }
	public int EligibleCount { get; set; }
	public int WaitlistedCount { get; set; }
	public int RejectedCount { get; set; }
	public int MeritRankedCount { get; set; }
	public int CapacityOverrideCount { get; set; }
	public int EligibilityOverrideCount { get; set; }
	public decimal AttendanceRate { get; set; }
	public decimal PassRate { get; set; }
	public decimal EligibilityRate { get; set; }
	public decimal WaitlistRate { get; set; }
	public decimal RejectionRate { get; set; }
	public decimal AveragePercentage { get; set; }
	public double? HighestPercentage { get; set; }
	public double? LowestPercentage { get; set; }
	public int SlotCount { get; set; }
	public int TotalSlotCapacity { get; set; }
	public int OverCapacitySlotCount { get; set; }
	public decimal SlotUtilizationRate { get; set; }
	public IReadOnlyList<AdmissionCrmExamClassPerformanceDto> ClassPerformance { get; set; } = [];
}

public class AdmissionCrmRegistrationAnalyticsDto
{
	public int IssuedCount { get; set; }
	public int OpenedCount { get; set; }
	public int StartedCount { get; set; }
	public int SubmittedCount { get; set; }
	public int VerifiedCount { get; set; }
	public int AdmissionReadyCount { get; set; }
	public int ExpiredCount { get; set; }
	public int CorrectionPendingCount { get; set; }
	public int AbandonedCount { get; set; }
	public decimal OpenRate { get; set; }
	public decimal CompletionRate { get; set; }
	public decimal VerificationRate { get; set; }
	public decimal AdmissionYieldRate { get; set; }
	public decimal AverageCompletionPercentage { get; set; }
}

public class AdmissionCrmDocumentAnalyticsDto
{
	public int AssignedChecklistCount { get; set; }
	public int UploadedCount { get; set; }
	public int UnderReviewCount { get; set; }
	public int VerifiedCount { get; set; }
	public int RejectedCount { get; set; }
	public int WaivedCount { get; set; }
	public int OriginalSeenCount { get; set; }
	public int ReadinessConfirmedCount { get; set; }
	public int ReadinessOverrideCount { get; set; }
	public decimal RejectionRate { get; set; }
	public decimal AverageReviewAgeDays { get; set; }
}

public class AdmissionCrmExamClassPerformanceDto
{
	public Guid? TargetStandardId { get; set; }
	public string ClassName { get; set; } = string.Empty;
	public int ScheduledCount { get; set; }
	public int PresentCount { get; set; }
	public int AbsentCount { get; set; }
	public int PassCount { get; set; }
	public int FailCount { get; set; }
	public int EligibleCount { get; set; }
	public int WaitlistedCount { get; set; }
	public int RejectedCount { get; set; }
	public int MeritRankedCount { get; set; }
	public decimal AttendanceRate { get; set; }
	public decimal PassRate { get; set; }
	public decimal EligibilityRate { get; set; }
	public decimal AveragePercentage { get; set; }
}

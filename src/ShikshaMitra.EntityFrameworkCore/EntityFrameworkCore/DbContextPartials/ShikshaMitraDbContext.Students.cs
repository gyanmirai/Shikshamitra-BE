using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.Compliance;
using ShikshaMitra.Students.Documents;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.Records.Settings;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	public DbSet<AdmissionAssessmentSession> AdmissionAssessmentSessions { get; set; }
	public DbSet<AdmissionAssessmentSessionScope> AdmissionAssessmentSessionScopes { get; set; }
	public DbSet<AdmissionAssessmentBookingBatch> AdmissionAssessmentBookingBatches { get; set; }
	public DbSet<AdmissionAssessmentAttempt> AdmissionAssessmentAttempts { get; set; }
    // ───────────────────────────────────────────────
    // Students — Core
    // ───────────────────────────────────────────────
    public DbSet<Student> Students { get; set; }
    public DbSet<StudentAdditionalDetail> StudentAdditionalDetails { get; set; }
    public DbSet<StudentContactDetail> StudentContactDetails { get; set; }
    public DbSet<StudentFamilyDetail> StudentFamilyDetails { get; set; }
    public DbSet<StudentCommunicationDetail> StudentCommunicationDetails { get; set; }
    public DbSet<StudentMedicalRecord> StudentMedicalRecords { get; set; }
    public DbSet<StudentPreviousSchoolDetail> StudentPreviousSchoolDetails { get; set; }
    public DbSet<StudentBankDetail> StudentBankDetails { get; set; }
    public DbSet<StudentDocument> StudentDocuments { get; set; }
    public DbSet<StudentIdentityCompliance> StudentIdentityCompliances { get; set; }
    public DbSet<StudentDocumentAccessAudit> StudentDocumentAccessAudits { get; set; }
    public DbSet<StudentNote> StudentNotes { get; set; }
    public DbSet<StudentEnrollment> StudentEnrollments { get; set; }

    // ───────────────────────────────────────────────
    // Students — Document and admission-support settings
    // ───────────────────────────────────────────────
    public DbSet<StudentDocumentSetting> StudentDocumentSettings { get; set; }
    public DbSet<InquiryPreference> InquiryPreferences { get; set; }
    public DbSet<EntranceExamSlot> EntranceExamSlots { get; set; }
	public DbSet<DocumentType> DocumentTypes { get; set; }
	public DbSet<AdmissionCycle> AdmissionCycles { get; set; }
	public DbSet<AdmissionCycleHistory> AdmissionCycleHistories { get; set; }
	public DbSet<AdmissionCampus> AdmissionCampuses { get; set; }
	public DbSet<AdmissionOffering> AdmissionOfferings { get; set; }
	public DbSet<AdmissionClassRoomAllocation> AdmissionClassRoomAllocations { get; set; }
	public DbSet<AdmissionReferenceSequence> AdmissionReferenceSequences { get; set; }
	public DbSet<AdmissionInquiry> AdmissionInquiries { get; set; }
	public DbSet<AdmissionWorkflowDefinition> AdmissionWorkflowDefinitions { get; set; }
	public DbSet<AdmissionWorkflowStageDefinition> AdmissionWorkflowStageDefinitions { get; set; }
	public DbSet<AdmissionWorkflowTransitionDefinition> AdmissionWorkflowTransitionDefinitions { get; set; }
	public DbSet<AdmissionWorkflowGovernanceSetting> AdmissionWorkflowGovernanceSettings { get; set; }
	public DbSet<AdmissionWorkflowGovernanceHistory> AdmissionWorkflowGovernanceHistories { get; set; }
	public DbSet<AdmissionWorkflowNotification> AdmissionWorkflowNotifications { get; set; }
	public DbSet<AdmissionFormDefinition> AdmissionFormDefinitions { get; set; }
	public DbSet<AdmissionFormSectionDefinition> AdmissionFormSectionDefinitions { get; set; }
	public DbSet<AdmissionFormFieldDefinition> AdmissionFormFieldDefinitions { get; set; }
	public DbSet<AdmissionFamily> AdmissionFamilies { get; set; }
	public DbSet<AdmissionGuardian> AdmissionGuardians { get; set; }
	public DbSet<AdmissionApplicant> AdmissionApplicants { get; set; }
	public DbSet<AdmissionApplication> AdmissionApplications { get; set; }
	public DbSet<AdmissionApplicationWorkflowInstance> AdmissionApplicationWorkflowInstances { get; set; }
	public DbSet<AdmissionApplicationStageInstance> AdmissionApplicationStageInstances { get; set; }
	public DbSet<AdmissionWorkflowTransitionHistory> AdmissionWorkflowTransitionHistory { get; set; }
	public DbSet<AdmissionFormInstance> AdmissionFormInstances { get; set; }
	public DbSet<AdmissionFormFieldValue> AdmissionFormFieldValues { get; set; }
	public DbSet<AdmissionFormSubmission> AdmissionFormSubmissions { get; set; }
	public DbSet<AdmissionFormChangeHistory> AdmissionFormChangeHistories { get; set; }
	public DbSet<AdmissionActivity> AdmissionActivities { get; set; }
	public DbSet<AdmissionInquiryManagement> AdmissionInquiryManagement { get; set; }
	public DbSet<AdmissionIntakeProfile> AdmissionIntakeProfiles { get; set; }
	public DbSet<AdmissionIntakeSetup> AdmissionIntakeSetups { get; set; }
	public DbSet<AdmissionPublicInquiryLink> AdmissionPublicInquiryLinks { get; set; }
	public DbSet<AdmissionInquiryFollowUp> AdmissionInquiryFollowUps { get; set; }
	public DbSet<AdmissionDocumentEvidence> AdmissionDocumentEvidence { get; set; }
	public DbSet<AdmissionDocumentReview> AdmissionDocumentReviews { get; set; }
	public DbSet<AdmissionNote> AdmissionNotes { get; set; }
	public DbSet<AdmissionApplicationDecisionHistory> AdmissionApplicationDecisionHistories { get; set; }
	public DbSet<AdmissionEligibilityEvaluation> AdmissionEligibilityEvaluations { get; set; }
	public DbSet<AdmissionEligibilityException> AdmissionEligibilityExceptions { get; set; }
	public DbSet<AdmissionReviewWorkItem> AdmissionReviewWorkItems { get; set; }
	public DbSet<AdmissionReviewRecord> AdmissionReviewRecords { get; set; }
	public DbSet<AdmissionReviewDecision> AdmissionReviewDecisions { get; set; }
	public DbSet<AdmissionSeatPool> AdmissionSeatPools { get; set; }
	public DbSet<AdmissionSeatReservation> AdmissionSeatReservations { get; set; }
	public DbSet<StudentOnboardingCase> StudentOnboardingCases { get; set; }
	public DbSet<StudentOnboardingStep> StudentOnboardingSteps { get; set; }
	public DbSet<StudentOnboardingEvent> StudentOnboardingEvents { get; set; }
	public DbSet<StudentOnboardingPolicy> StudentOnboardingPolicies { get; set; }
	public DbSet<StudentOnboardingSettings> StudentOnboardingSettings { get; set; }
	public DbSet<StudentRollNumberBatch> StudentRollNumberBatches { get; set; }
	public DbSet<ShikshaMitra.Students.Portal.StudentPortalAccountLink> StudentPortalAccountLinks { get; set; }
	public DbSet<StudentSeatReservation> StudentSeatReservations { get; set; }
	public DbSet<StudentIdentifierSeries> StudentIdentifierSeries { get; set; }
	public DbSet<StudentIdentifierAllocation> StudentIdentifierAllocations { get; set; }
	public DbSet<StudentOnboardingServiceHandoff> StudentOnboardingServiceHandoffs { get; set; }

    // ───────────────────────────────────────────────
    // Students — Records
    // ───────────────────────────────────────────────
    public DbSet<HouseSettings> HouseSettings { get; set; }
    public DbSet<StudentStatusHistory> StudentStatusHistories { get; set; }
    public DbSet<StudentDashboardPreview> StudentDashboardPreviews { get; set; }
    public DbSet<StudentCertificate> StudentCertificates { get; set; }
    public DbSet<StudentCertificateTemplate> StudentCertificateTemplates { get; set; }
	public DbSet<StudentRecordChange> StudentRecordChanges { get; set; }
	public DbSet<StudentRecordCustomProfile> StudentRecordCustomProfiles { get; set; }
	public DbSet<StudentRecordChangeDecision> StudentRecordChangeDecisions { get; set; }
	public DbSet<StudentRecordDocumentEvent> StudentRecordDocumentEvents { get; set; }
	public DbSet<StudentRecordPhotoDraft> StudentRecordPhotoDrafts { get; set; }
	public DbSet<StudentRecordReportJob> StudentRecordReportJobs { get; set; }
	public DbSet<StudentRecordDuplicateReview> StudentRecordDuplicateReviews { get; set; }
	public DbSet<StudentRecordTransition> StudentRecordTransitions { get; set; }
	public DbSet<StudentRecordTransitionBatch> StudentRecordTransitionBatches { get; set; }
	public DbSet<StudentRecordTransitionEvent> StudentRecordTransitionEvents { get; set; }
	public DbSet<StudentRecordRenewalCase> StudentRecordRenewalCases { get; set; }
	public DbSet<StudentRecordReturnHandoff> StudentRecordReturnHandoffs { get; set; }
	public DbSet<StudentGuardianAccessReview> StudentGuardianAccessReviews { get; set; }
	public DbSet<StudentIdCard> StudentIdCards { get; set; }
	public DbSet<StudentIdCardTemplate> StudentIdCardTemplates { get; set; }
	public DbSet<StudentRecordLegacyOverride> StudentRecordLegacyOverrides { get; set; }
	public DbSet<ShikshaMitra.Students.StudentRecords.StudentRecordsConfiguration> StudentRecordsConfigurations { get; set; }
	public DbSet<ShikshaMitra.Students.StudentRecords.StudentRecordsPolicyVersion> StudentRecordsPolicyVersions { get; set; }

    // ───────────────────────────────────────────────
    // Students — Attendance
    // ───────────────────────────────────────────────
    public DbSet<AttendanceDay> AttendanceDays { get; set; }
    public DbSet<AttendanceEntry> AttendanceEntries { get; set; }
    public DbSet<ShikshaMitra.Students.Attendance.Settings.StudentLeaveType> StudentLeaveTypes { get; set; }
    public DbSet<ShikshaMitra.Students.Attendance.StudentLeave> StudentLeaves { get; set; }
}

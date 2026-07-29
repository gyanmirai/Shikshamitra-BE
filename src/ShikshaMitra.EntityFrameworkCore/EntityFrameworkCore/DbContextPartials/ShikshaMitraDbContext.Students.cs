using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.Compliance;
using ShikshaMitra.Students.Documents;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.Records.Settings;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using ShikshaMitra.Students.StudentEnrollments.Settings;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
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
    public DbSet<AdmissionInquiryLead> AdmissionInquiryLeads { get; set; }
    public DbSet<AdmissionCrmInquirySource> AdmissionCrmInquirySources { get; set; }
    public DbSet<AdmissionCrmInquiryType> AdmissionCrmInquiryTypes { get; set; }
    public DbSet<AdmissionCrmExamSlot> AdmissionCrmExamSlots { get; set; }
    public DbSet<AdmissionCrmDocumentType> AdmissionCrmDocumentTypes { get; set; }
    public DbSet<AdmissionCrmCycleConfiguration> AdmissionCrmCycleConfigurations { get; set; }
    public DbSet<AdmissionCrmAdmissionYearSetup> AdmissionCrmAdmissionYearSetups { get; set; }
    public DbSet<AdmissionCrmPipelineStage> AdmissionCrmPipelineStages { get; set; }
    public DbSet<AdmissionInquiryFormTemplate> AdmissionInquiryFormTemplates { get; set; }
    public DbSet<AdmissionInquiryFormSubmission> AdmissionInquiryFormSubmissions { get; set; }
    public DbSet<AdmissionInquiryConsentRecord> AdmissionInquiryConsentRecords { get; set; }
    public DbSet<AdmissionApplicantStatusAccess> AdmissionApplicantStatusAccesses { get; set; }
    public DbSet<AdmissionCrmApplicantChecklistProgress> AdmissionCrmApplicantChecklistProgresses { get; set; }
    public DbSet<AdmissionCrmFollowUpTask> AdmissionCrmFollowUpTasks { get; set; }
    public DbSet<AdmissionCrmEntranceExamRecord> AdmissionCrmEntranceExamRecords { get; set; }
    public DbSet<AdmissionCrmMessageTemplate> AdmissionCrmMessageTemplates { get; set; }
    public DbSet<AdmissionCrmCommunicationProviderConfiguration> AdmissionCrmCommunicationProviderConfigurations { get; set; }
    public DbSet<AdmissionCrmCommunicationOptOut> AdmissionCrmCommunicationOptOuts { get; set; }
    public DbSet<AdmissionCrmMessageDeliveryLog> AdmissionCrmMessageDeliveryLogs { get; set; }
    public DbSet<AdmissionCrmTimelineEvent> AdmissionCrmTimelineEvents { get; set; }
    public DbSet<AdmissionCrmAutomationRun> AdmissionCrmAutomationRuns { get; set; }
    public DbSet<AdmissionCrmPlaybook> AdmissionCrmPlaybooks { get; set; }
    public DbSet<AdmissionCrmRegistrationTemplate> AdmissionCrmRegistrationTemplates { get; set; }
    public DbSet<AdmissionCrmFormNumberSeries> AdmissionCrmFormNumberSeries { get; set; }
    public DbSet<AdmissionCrmRegistrationApplication> AdmissionCrmRegistrationApplications { get; set; }
    public DbSet<AdmissionCrmDocumentChecklistTemplate> AdmissionCrmDocumentChecklistTemplates { get; set; }
    public DbSet<AdmissionCrmDocumentChecklistAssignment> AdmissionCrmDocumentChecklistAssignments { get; set; }
    public DbSet<AdmissionCrmApplicantDocument> AdmissionCrmApplicantDocuments { get; set; }
    public DbSet<AdmissionCrmSeatPool> AdmissionCrmSeatPools { get; set; }
    public DbSet<AdmissionCrmSeatReservation> AdmissionCrmSeatReservations { get; set; }
    public DbSet<AdmissionCrmSeatOffer> AdmissionCrmSeatOffers { get; set; }
    public DbSet<AdmissionCrmWaitlistEntry> AdmissionCrmWaitlistEntries { get; set; }
    public DbSet<AdmissionCrmWaitlistHistory> AdmissionCrmWaitlistHistories { get; set; }
    public DbSet<AdmissionCrmAdmissionDecision> AdmissionCrmAdmissionDecisions { get; set; }
    public DbSet<AdmissionCrmFeePolicy> AdmissionCrmFeePolicies { get; set; }
    public DbSet<AdmissionCrmAdmissionQuotation> AdmissionCrmAdmissionQuotations { get; set; }
    public DbSet<AdmissionCrmConcessionRequest> AdmissionCrmConcessionRequests { get; set; }
    public DbSet<AdmissionCrmPerson> AdmissionCrmPersons { get; set; }
    public DbSet<AdmissionCrmHousehold> AdmissionCrmHouseholds { get; set; }
    public DbSet<AdmissionCrmHouseholdMembership> AdmissionCrmHouseholdMemberships { get; set; }
    public DbSet<AdmissionCrmApplicantIdentityProfile> AdmissionCrmApplicantIdentityProfiles { get; set; }
    public DbSet<AdmissionCrmGuardianRelationship> AdmissionCrmGuardianRelationships { get; set; }
    public DbSet<AdmissionCrmIdentityConsent> AdmissionCrmIdentityConsents { get; set; }
    public DbSet<AdmissionCrmPolicyPack> AdmissionCrmPolicyPacks { get; set; }
    public DbSet<AdmissionCrmPolicyAdoption> AdmissionCrmPolicyAdoptions { get; set; }
    public DbSet<AdmissionCrmPolicyEvaluation> AdmissionCrmPolicyEvaluations { get; set; }
    public DbSet<AdmissionCrmPolicyException> AdmissionCrmPolicyExceptions { get; set; }
    public DbSet<AdmissionCrmNumberSeries> AdmissionCrmNumberSeries { get; set; }
    public DbSet<AdmissionCrmIdentifierAllocation> AdmissionCrmIdentifierAllocations { get; set; }
    public DbSet<AdmissionCrmActivation> AdmissionCrmActivations { get; set; }
    public DbSet<AdmissionCrmServiceHandoff> AdmissionCrmServiceHandoffs { get; set; }
      public DbSet<AdmissionCrmRouteDefinition> AdmissionCrmRouteDefinitions { get; set; }
      public DbSet<AdmissionCrmRouteAssignment> AdmissionCrmRouteAssignments { get; set; }
      public DbSet<AdmissionCrmRouteAppeal> AdmissionCrmRouteAppeals { get; set; }
    public DbSet<AdmissionCrmDataQualityIssue> AdmissionCrmDataQualityIssues { get; set; }
    public DbSet<AdmissionCrmRetentionPolicy> AdmissionCrmRetentionPolicies { get; set; }
    public DbSet<AdmissionCrmPrivacyRequest> AdmissionCrmPrivacyRequests { get; set; }
    public DbSet<AdmissionCrmExportRequest> AdmissionCrmExportRequests { get; set; }
    public DbSet<AdmissionCrmReportSchedule> AdmissionCrmReportSchedules { get; set; }
    public DbSet<AdmissionCrmWorkDelegation> AdmissionCrmWorkDelegations { get; set; }
    public DbSet<AdmissionCrmOperationsSavedView> AdmissionCrmOperationsSavedViews { get; set; }
    public DbSet<AdmissionCrmSlaHold> AdmissionCrmSlaHolds { get; set; }
    public DbSet<AdmissionCrmSettingsAccessPolicy> AdmissionCrmSettingsAccessPolicies { get; set; }
    public DbSet<AdmissionCrmPlatformSupportSession> AdmissionCrmPlatformSupportSessions { get; set; }
    public DbSet<AdmissionCrmPlatformPreset> AdmissionCrmPlatformPresets { get; set; }
    public DbSet<AdmissionCrmPresetAdoption> AdmissionCrmPresetAdoptions { get; set; }

    // ───────────────────────────────────────────────
    // Students — Enrollment Settings
    // ───────────────────────────────────────────────
    public DbSet<StudentDocumentSetting> StudentDocumentSettings { get; set; }
    public DbSet<InquiryPreference> InquiryPreferences { get; set; }
    public DbSet<EntranceExamSlot> EntranceExamSlots { get; set; }
    public DbSet<DocumentType> DocumentTypes { get; set; }

    // ───────────────────────────────────────────────
    // Students — Records
    // ───────────────────────────────────────────────
    public DbSet<HouseSettings> HouseSettings { get; set; }
    public DbSet<StudentManagementSettings> StudentManagementSettings { get; set; }
    public DbSet<StudentStatusHistory> StudentStatusHistories { get; set; }
    public DbSet<StudentDashboardPreview> StudentDashboardPreviews { get; set; }
    public DbSet<AcademicTerm> AcademicTerms { get; set; }
    public DbSet<StudentRollNumberAssignment> StudentRollNumberAssignments { get; set; }
    public DbSet<StudentCertificate> StudentCertificates { get; set; }
    public DbSet<StudentCertificateTemplate> StudentCertificateTemplates { get; set; }

    // ───────────────────────────────────────────────
    // Students — Attendance
    // ───────────────────────────────────────────────
    public DbSet<AttendanceDay> AttendanceDays { get; set; }
    public DbSet<AttendanceEntry> AttendanceEntries { get; set; }
    public DbSet<ShikshaMitra.Students.Attendance.Settings.StudentLeaveType> StudentLeaveTypes { get; set; }
    public DbSet<ShikshaMitra.Students.Attendance.StudentLeave> StudentLeaves { get; set; }
}

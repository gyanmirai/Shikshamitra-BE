using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.Compliance;
using ShikshaMitra.Students.Documents;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.Records.Settings;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using ShikshaMitra.Students.StudentEnrollments.Admissions;

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

    // ───────────────────────────────────────────────
    // Students — Document and admission-support settings
    // ───────────────────────────────────────────────
    public DbSet<StudentDocumentSetting> StudentDocumentSettings { get; set; }
    public DbSet<InquiryPreference> InquiryPreferences { get; set; }
    public DbSet<EntranceExamSlot> EntranceExamSlots { get; set; }
	public DbSet<DocumentType> DocumentTypes { get; set; }
	public DbSet<AdmissionCycle> AdmissionCycles { get; set; }
	public DbSet<AdmissionOffering> AdmissionOfferings { get; set; }
	public DbSet<AdmissionInquiry> AdmissionInquiries { get; set; }
	public DbSet<AdmissionWorkflowDefinition> AdmissionWorkflowDefinitions { get; set; }
	public DbSet<AdmissionWorkflowStageDefinition> AdmissionWorkflowStageDefinitions { get; set; }
	public DbSet<AdmissionWorkflowTransitionDefinition> AdmissionWorkflowTransitionDefinitions { get; set; }
	public DbSet<AdmissionFormDefinition> AdmissionFormDefinitions { get; set; }
	public DbSet<AdmissionFormSectionDefinition> AdmissionFormSectionDefinitions { get; set; }
	public DbSet<AdmissionFormFieldDefinition> AdmissionFormFieldDefinitions { get; set; }
	public DbSet<AdmissionFamily> AdmissionFamilies { get; set; }
	public DbSet<AdmissionGuardian> AdmissionGuardians { get; set; }
	public DbSet<AdmissionApplicant> AdmissionApplicants { get; set; }
	public DbSet<AdmissionApplication> AdmissionApplications { get; set; }
	public DbSet<AdmissionApplicationWorkflowInstance> AdmissionApplicationWorkflowInstances { get; set; }
	public DbSet<AdmissionApplicationStageInstance> AdmissionApplicationStageInstances { get; set; }
	public DbSet<AdmissionFormInstance> AdmissionFormInstances { get; set; }
	public DbSet<AdmissionFormFieldValue> AdmissionFormFieldValues { get; set; }
	public DbSet<AdmissionActivity> AdmissionActivities { get; set; }
	public DbSet<AdmissionNote> AdmissionNotes { get; set; }

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

using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.Compliance;
using ShikshaMitra.Students.Documents;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.Records.Settings;
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

    // ───────────────────────────────────────────────
    // Students — Attendance
    // ───────────────────────────────────────────────
    public DbSet<AttendanceDay> AttendanceDays { get; set; }
    public DbSet<AttendanceEntry> AttendanceEntries { get; set; }
    public DbSet<ShikshaMitra.Students.Attendance.Settings.StudentLeaveType> StudentLeaveTypes { get; set; }
    public DbSet<ShikshaMitra.Students.Attendance.StudentLeave> StudentLeaves { get; set; }
}

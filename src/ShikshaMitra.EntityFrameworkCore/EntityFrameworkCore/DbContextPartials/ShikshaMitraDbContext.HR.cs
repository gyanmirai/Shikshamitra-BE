using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Communication.AnnouncementTypes;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Calendar.Events;
using ShikshaMitra.HR.Calendar.EventTypes;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Calendar.HolidayTypes;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.ICards;
using ShikshaMitra.HR.Employees.Onboarding;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.AttendanceImports;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Employees.Settings;
using ShikshaMitra.HR.Audit;
using ShikshaMitra.HR.Dashboard;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.ProfessionalManagement;
using ShikshaMitra.HR.Reports;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
    DbSet<Employee> Employees { get; set; }
    public DbSet<EmployeeApplication> EmployeeApplications { get; set; }
    DbSet<EmployeeAdditionalDetails> EmployeeAdditionalDetails { get; set; }
    DbSet<EmployeeFamilyMember> EmployeeFamilyMembers { get; set; }
    public DbSet<EmployeeOrganizationAssignment> EmployeeOrganizationAssignments { get; set; }
    DbSet<DocumentSetting> DocumentSettings { get; set; }
    public DbSet<EmployeeICardTemplate> EmployeeICardTemplates { get; set; }

    public DbSet<EmployeePayrollDetails> EmployeePayrollDetails { get; set; }
    public DbSet<EmployeePayrollComponent> EmployeePayrollComponents { get; set; }

    DbSet<EmployeeAttendance> EmployeeAttendances { get; set; }
    DbSet<StaffAttendanceDayLock> StaffAttendanceDayLocks { get; set; }
    public DbSet<EmployeeLeave> EmployeeLeaves { get; set; }
    DbSet<AutomatedNotificationsSettings> AutomatedNotificationsSettings { get; set; }
    DbSet<NotificationInfo> NotificationInfos { get; set; }
    public DbSet<LeavePolicy> EmployeeLeavePolicies { get; set; }
    public DbSet<LeavePolicyRule> EmployeeLeavePolicyRules { get; set; }

    DbSet<PayElement> PayElements { get; set; }
    DbSet<ProvidentFund> ProvidentFunds { get; set; }
    DbSet<ProfessionalTax> ProfessionalTaxes { get; set; }
    public DbSet<EarningComponent> EarningComponents { get; set; }
    public DbSet<DeductionComponent> DeductionComponents { get; set; }
    public DbSet<PayrollComponentVersion> PayrollComponentVersions { get; set; }
    public DbSet<SalarySlip> SalarySlips { get; set; }
    public DbSet<AttendanceImportBatch> AttendanceImportBatches { get; set; }
    public DbSet<AttendanceImportRow> AttendanceImportRows { get; set; }
    public DbSet<PayrollRunReview> PayrollRunReviews { get; set; }
    public DbSet<PayrollRunItemSnapshot> PayrollRunItemSnapshots { get; set; }
    public DbSet<PayrollAdjustment> PayrollAdjustments { get; set; }
    public DbSet<PayrollMonthLock> PayrollMonthLocks { get; set; }

    public DbSet<WorkCalendarPolicy> WorkCalendarPolicies { get; set; }
    public DbSet<WorkCalendarHoliday> WorkCalendarHolidays { get; set; }
    public DbSet<AttendancePolicy> AttendancePolicies { get; set; }
    public DbSet<PayrollPolicy> PayrollPolicies { get; set; }
    public DbSet<HrPolicyAssignment> HrPolicyAssignments { get; set; }
    public DbSet<HrPolicyVersion> HrPolicyVersions { get; set; }
    public DbSet<HrAuditEvent> HrAuditEvents { get; set; }
    public DbSet<HrDashboardPreview> HrDashboardPreviews { get; set; }
    public DbSet<HrDashboardRefreshRun> HrDashboardRefreshRuns { get; set; }
    public DbSet<ProfessionalReviewCycle> ProfessionalReviewCycles { get; set; }
    public DbSet<PerformanceTemplate> PerformanceTemplates { get; set; }
    public DbSet<PerformanceTemplateSection> PerformanceTemplateSections { get; set; }
    public DbSet<PerformanceTemplateCriterion> PerformanceTemplateCriteria { get; set; }
    public DbSet<PerformanceReviewAssignment> PerformanceReviewAssignments { get; set; }
    public DbSet<PerformanceReviewStage> PerformanceReviewStages { get; set; }
    public DbSet<PerformanceReviewResponse> PerformanceReviewResponses { get; set; }
    public DbSet<ClassroomObservation> ClassroomObservations { get; set; }
    public DbSet<PerformanceEvidence> PerformanceEvidence { get; set; }
    public DbSet<PerformanceFeedback> PerformanceFeedback { get; set; }
    public DbSet<PerformanceOutcome> PerformanceOutcomes { get; set; }
    public DbSet<DevelopmentPlan> DevelopmentPlans { get; set; }
    public DbSet<PerformanceAuditEvent> PerformanceAuditEvents { get; set; }
    public DbSet<HrReportExportJob> HrReportExportJobs { get; set; }
    public DbSet<HrReportSchedule> HrReportSchedules { get; set; }
    public DbSet<HrReportTemplate> HrReportTemplates { get; set; }

    public DbSet<HolidayType> HolidayTypes { get; set; }
    public DbSet<EventType> EventTypes { get; set; }
    public DbSet<Holiday> Holidays { get; set; }
    public DbSet<Event> Events { get; set; }

    public DbSet<AnnouncementType> AnnouncementTypes { get; set; }

    public DbSet<ShikshaMitra.Groups.Group> Groups { get; set; }
    public DbSet<ShikshaMitra.Groups.GroupStudentRule> GroupStudentRules { get; set; }
    public DbSet<ShikshaMitra.Groups.GroupEmployeeRule> GroupEmployeeRules { get; set; }
    public DbSet<ShikshaMitra.Groups.GroupMember> GroupMembers { get; set; }
}

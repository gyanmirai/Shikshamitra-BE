using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Communication.AnnouncementTypes;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Calendar.Events;
using ShikshaMitra.HR.Calendar.EventTypes;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Calendar.HolidayTypes;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.AttendanceImports;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Employees.Settings;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Settings;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
    DbSet<Employee> Employees { get; set; }
    DbSet<EmployeeAdditionalDetails> EmployeeAdditionalDetails { get; set; }
    DbSet<EmployeeFamilyMember> EmployeeFamilyMembers { get; set; }
    DbSet<DocumentSetting> DocumentSettings { get; set; }

    public DbSet<EmployeePayrollDetails> EmployeePayrollDetails { get; set; }
    public DbSet<EmployeePayrollComponent> EmployeePayrollComponents { get; set; }

    DbSet<EmployeeAttendance> EmployeeAttendances { get; set; }
    DbSet<StaffAttendanceDayLock> StaffAttendanceDayLocks { get; set; }
    public DbSet<EmployeeLeave> EmployeeLeaves { get; set; }
    DbSet<AttendanceConfiguration> AttendanceConfigurations { get; set; }
    DbSet<AutomatedNotificationsSettings> AutomatedNotificationsSettings { get; set; }
    DbSet<NotificationInfo> NotificationInfos { get; set; }
    public DbSet<LeaveType> EmployeeLeaveTypes { get; set; }
    public DbSet<LeavePolicy> EmployeeLeavePolicies { get; set; }

    DbSet<PayElement> PayElements { get; set; }
    DbSet<ProvidentFund> ProvidentFunds { get; set; }
    DbSet<ProfessionalTax> ProfessionalTaxes { get; set; }
    public DbSet<EarningComponent> EarningComponents { get; set; }
    public DbSet<DeductionComponent> DeductionComponents { get; set; }
    public DbSet<PayrollComponentVersion> PayrollComponentVersions { get; set; }
    public DbSet<DayCalculationSetting> DayCalculationSettings { get; set; }
    public DbSet<SalarySlip> SalarySlips { get; set; }
    public DbSet<AttendanceImportBatch> AttendanceImportBatches { get; set; }
    public DbSet<AttendanceImportRow> AttendanceImportRows { get; set; }
    public DbSet<PayrollRunReview> PayrollRunReviews { get; set; }
    public DbSet<PayrollRunItemSnapshot> PayrollRunItemSnapshots { get; set; }

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

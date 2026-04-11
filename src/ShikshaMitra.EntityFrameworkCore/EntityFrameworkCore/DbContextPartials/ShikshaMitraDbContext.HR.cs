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
using ShikshaMitra.HR.Employees.Settings;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Settings;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
    // ───────────────────────────────────────────────
    // HR — Employees
    // ───────────────────────────────────────────────
    DbSet<Employee> Employees { get; set; }
    DbSet<EmployeeAdditionalDetails> EmployeeAdditionalDetails { get; set; }
    DbSet<EmployeeFamilyMember> EmployeeFamilyMembers { get; set; }
    DbSet<DocumentSetting> DocumentSettings { get; set; }

    // ───────────────────────────────────────────────
    // HR — Employee Payroll
    // ───────────────────────────────────────────────
    public DbSet<EmployeePayrollDetails> EmployeePayrollDetails { get; set; }
    public DbSet<EmployeePayrollComponent> EmployeePayrollComponents { get; set; }

    // ───────────────────────────────────────────────
    // HR — Attendance & Leaves
    // ───────────────────────────────────────────────
    DbSet<EmployeeAttendance> EmployeeAttendances { get; set; }
    public DbSet<EmployeeLeave> EmployeeLeaves { get; set; }
    DbSet<CustomAttendanceRule> CustomAttendanceRules { get; set; }
    DbSet<AutomatedNotificationsSettings> AutomatedNotificationsSettings { get; set; }
    DbSet<NotificationInfo> NotificationInfos { get; set; }
    public DbSet<LeaveType> EmployeeLeaveTypes { get; set; }
    public DbSet<LeavePolicy> EmployeeLeavePolicies { get; set; }

    // ───────────────────────────────────────────────
    // HR — Payroll Settings & Salary Slips
    // ───────────────────────────────────────────────
    DbSet<PayElement> PayElements { get; set; }
    DbSet<ProvidentFund> ProvidentFunds { get; set; }
    DbSet<ProfessionalTax> ProfessionalTaxes { get; set; }
    public DbSet<EarningComponent> EarningComponents { get; set; }
    public DbSet<DeductionComponent> DeductionComponents { get; set; }
    public DbSet<DayCalculationSetting> DayCalculationSettings { get; set; }
    public DbSet<SalarySlip> SalarySlips { get; set; }

    // ───────────────────────────────────────────────
    // HR — Events & Holidays
    // ───────────────────────────────────────────────
    public DbSet<HolidayType> HolidayTypes { get; set; }
    public DbSet<EventType> EventTypes { get; set; }
    public DbSet<Holiday> Holidays { get; set; }
    public DbSet<Event> Events { get; set; }

    // ───────────────────────────────────────────────
    // HR — Communication
    // ───────────────────────────────────────────────
    public DbSet<AnnouncementType> AnnouncementTypes { get; set; }

    // ───────────────────────────────────────────────
    // HR — Groups
    // ───────────────────────────────────────────────
    public DbSet<ShikshaMitra.Groups.Group> Groups { get; set; }
    public DbSet<ShikshaMitra.Groups.GroupStudentRule> GroupStudentRules { get; set; }
    public DbSet<ShikshaMitra.Groups.GroupEmployeeRule> GroupEmployeeRules { get; set; }
    public DbSet<ShikshaMitra.Groups.GroupMember> GroupMembers { get; set; }
}

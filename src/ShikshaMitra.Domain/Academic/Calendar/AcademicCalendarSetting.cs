using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Calendar;

public class AcademicCalendarSetting : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid? AcademicMediumId { get; private set; }
    public Guid? AcademicYearId { get; private set; }
    public WeekendRule WeekendRule { get; set; }
    
    public bool AllowClassSpecificHolidays { get; set; }
    public bool AllowDivisionSpecificHolidays { get; set; }
    public bool EnableHolidayCategories { get; set; }
    public bool EnableVacationRanges { get; set; }
    public bool EnableHalfDayRules { get; set; }
    
    public bool BlockTimetableOnHoliday { get; set; }
    public bool BlockTimetableOnVacation { get; set; }
    public bool BlockTimetableOnEmergencyClosure { get; set; }
    
    public bool BlockHomeworkDueDateOnHoliday { get; set; }
    public bool BlockHomeworkDueDateOnVacation { get; set; }
    
    public bool BlockExamScheduleOnHoliday { get; set; }
    public bool BlockExamScheduleOnVacation { get; set; }
    public bool AllowExamOnHalfDay { get; set; }
    
    public bool BlockLessonPlanOnHoliday { get; set; }
    public bool BlockLessonPlanOnVacation { get; set; }
    
    public bool BlockOnlineClassOnHoliday { get; set; }
    public bool BlockOnlineClassOnEventDay { get; set; }
    
    public bool AutoExemptAttendanceOnHoliday { get; set; }
    public bool AutoExemptAttendanceOnVacation { get; set; }
    
    public bool EnableEmergencyClosure { get; set; }
    public bool EmergencyClosureRequiresPrincipalApproval { get; set; }
    
    public bool AutoScanImpactedActivities { get; set; }
    public RescheduleMode RescheduleMode { get; set; }
    
    public bool NotifyTeachersOnCalendarChange { get; set; }
    public bool NotifyParentsOnCalendarChange { get; set; }
    public bool NotifyStudentsOnCalendarChange { get; set; }
    public bool NotifyStaffOnCalendarChange { get; set; }
    
    public CalendarVisibility CalendarVisibility { get; set; }
    public bool RequireReasonForPublishedCalendarChange { get; set; }

    private AcademicCalendarSetting()
    {
        /* ORM */
    }

    public AcademicCalendarSetting(Guid id, Guid? tenantId, Guid academicMediumId, Guid? academicYearId = null) : base(id)
    {
        TenantId = tenantId;
        AcademicMediumId = academicMediumId;
        AcademicYearId = academicYearId;
        
        // Defaults
        WeekendRule = WeekendRule.SundayOnly;
        AllowClassSpecificHolidays = true;
        AllowDivisionSpecificHolidays = true;
        EnableHolidayCategories = true;
        EnableVacationRanges = true;
        EnableHalfDayRules = true;
        
        BlockTimetableOnHoliday = true;
        BlockTimetableOnVacation = true;
        BlockTimetableOnEmergencyClosure = true;
        
        BlockHomeworkDueDateOnHoliday = true;
        BlockHomeworkDueDateOnVacation = true;
        
        BlockExamScheduleOnHoliday = true;
        BlockExamScheduleOnVacation = true;
        AllowExamOnHalfDay = false;
        
        BlockLessonPlanOnHoliday = true;
        BlockLessonPlanOnVacation = true;
        
        BlockOnlineClassOnHoliday = true;
        BlockOnlineClassOnEventDay = false;
        
        AutoExemptAttendanceOnHoliday = true;
        AutoExemptAttendanceOnVacation = true;
        
        EnableEmergencyClosure = true;
        EmergencyClosureRequiresPrincipalApproval = false;
        
        AutoScanImpactedActivities = true;
        RescheduleMode = RescheduleMode.SuggestNewDate;
        
        NotifyTeachersOnCalendarChange = true;
        NotifyParentsOnCalendarChange = true;
        NotifyStudentsOnCalendarChange = true;
        NotifyStaffOnCalendarChange = false;
        
        CalendarVisibility = CalendarVisibility.ParentsAndStudents;
        RequireReasonForPublishedCalendarChange = true;
    }
}

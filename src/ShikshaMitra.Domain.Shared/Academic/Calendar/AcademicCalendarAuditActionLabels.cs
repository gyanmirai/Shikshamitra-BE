using System.Collections.Generic;

namespace ShikshaMitra.Academic.Calendar;

/// <summary>Human-readable labels for CalendarAuditAction, mirrored on the FE (CALENDAR_AUDIT_ACTION_LABELS).</summary>
public static class AcademicCalendarAuditActionLabels
{
    private static readonly Dictionary<CalendarAuditAction, string> Labels = new()
    {
        [CalendarAuditAction.CalendarCreated] = "Calendar Created",
        [CalendarAuditAction.CalendarUpdated] = "Calendar Updated",
        [CalendarAuditAction.CalendarPublished] = "Calendar Published",
        [CalendarAuditAction.CalendarArchived] = "Calendar Archived",
        [CalendarAuditAction.DayAdded] = "Day Added",
        [CalendarAuditAction.DayUpdated] = "Day Updated",
        [CalendarAuditAction.DayDeleted] = "Day Deleted",
        [CalendarAuditAction.EmergencyClosureDeclared] = "Emergency Closure Declared",
        [CalendarAuditAction.ImpactResolved] = "Impact Resolved",
        [CalendarAuditAction.OverrideApplied] = "Override Applied",
        [CalendarAuditAction.CalendarDeleted] = "Calendar Deleted",
        [CalendarAuditAction.WorkingHourAdded] = "Working Hours Added",
        [CalendarAuditAction.WorkingHourUpdated] = "Working Hours Updated",
        [CalendarAuditAction.WorkingHourDeleted] = "Working Hours Deleted",
        [CalendarAuditAction.SettingsUpdated] = "Calendar Settings Updated"
    };

    public static string Get(CalendarAuditAction action) =>
        Labels.TryGetValue(action, out var label) ? label : action.ToString();
}

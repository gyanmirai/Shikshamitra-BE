using System.Collections.Generic;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public class ConflictCheckResult
{
    public ValidationPass RoomAvailability { get; set; } = new(true);
    public ValidationPass TeacherAvailability { get; set; } = new(true);
    public ValidationPass ClassAvailability { get; set; } = new(true);
    public ValidationPass MaintenanceCheck { get; set; } = new(true);
    public ValidationPass HolidayCalendar { get; set; } = new(true);
    
    public string OverallSeverity { get; set; } = "none"; // "critical", "warning", "none"
    public bool CanSave { get; set; } = true;
    public bool RequiresApproval { get; set; } = false;
    public List<ConflictItem> Conflicts { get; set; } = new();
}

public class ValidationPass
{
    public bool Passed { get; set; }
    public string? Message { get; set; }

    public ValidationPass(bool passed, string? message = null)
    {
        Passed = passed;
        Message = message;
    }
}

public class ConflictItem
{
    public string Type { get; set; }
    public string Severity { get; set; }
    public string Message { get; set; }
    public string Suggestion { get; set; }

    public ConflictItem(string type, string severity, string message, string suggestion)
    {
        Type = type;
        Severity = severity;
        Message = message;
        Suggestion = suggestion;
    }
}

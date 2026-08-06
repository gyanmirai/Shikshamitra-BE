using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public class RoomAvailabilityFilterDto
{
    public DateTime Date { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public Guid? LectureSlotId { get; set; }
    public Guid? BuildingId { get; set; }
    public Guid? FloorId { get; set; }
    public Guid? RoomTypeId { get; set; }
    public int? MinimumCapacity { get; set; }
    public Guid? StandardId { get; set; }
    public Guid? DivisionId { get; set; }
    public bool IncludeOccupied { get; set; } = true;
    public string? Search { get; set; }
}

public class RoomAvailabilityDto
{
    public Guid RoomId { get; set; }
    public string RoomName { get; set; }
    public string BuildingName { get; set; }
    public string FloorName { get; set; }
    public string Type { get; set; }
    public int Capacity { get; set; }
    public List<string> Facilities { get; set; } = new();
    public string Status { get; set; } // "available", "occupied", "reserved", "maintenance", "conflict"
    public string? OccupiedBy { get; set; }
    public string? ConflictReason { get; set; }
    public string? ConflictSeverity { get; set; }
    public string? Source { get; set; }
    public string? NextAvailableAt { get; set; }
    public int UtilizationPercentage { get; set; }
}

public class RoomAvailabilitySummaryDto
{
    public int FreeRooms { get; set; }
    public int FreeRoomPercentage { get; set; }
    public string? BestAvailableRoom { get; set; }
    public int BestAvailableRoomCapacity { get; set; }
    public int OccupiedRooms { get; set; }
    public string? NextFreeingRoom { get; set; }
    public string? NextFreeingTime { get; set; }
    public int ActiveReservations { get; set; }
    public int ConflictsVisible { get; set; }
}

public class CheckConflictRequestDto
{
    public Guid RoomId { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public Guid? TeacherId { get; set; }
    public Guid? StandardId { get; set; }
    public Guid? DivisionId { get; set; }
    public RoomReservationType ReservationType { get; set; }
}

public class ConflictCheckResultDto
{
    public ValidationPassDto RoomAvailability { get; set; } = new(true);
    public ValidationPassDto TeacherAvailability { get; set; } = new(true);
    public ValidationPassDto ClassAvailability { get; set; } = new(true);
    public ValidationPassDto MaintenanceCheck { get; set; } = new(true);
    public ValidationPassDto HolidayCalendar { get; set; } = new(true);

    public string OverallSeverity { get; set; } = "none";
    public bool CanSave { get; set; } = true;
    public bool RequiresApproval { get; set; } = false;
    public List<ConflictItemDto> Conflicts { get; set; } = new();
}

public class ValidationPassDto
{
    public bool Passed { get; set; }
    public string? Message { get; set; }
    public ValidationPassDto(bool passed, string? message = null)
    {
        Passed = passed;
        Message = message;
    }
}

public class ConflictItemDto
{
    public string Type { get; set; }
    public string Severity { get; set; }
    public string Message { get; set; }
    public string Suggestion { get; set; }

    public ConflictItemDto(string type, string severity, string message, string suggestion)
    {
        Type = type;
        Severity = severity;
        Message = message;
        Suggestion = suggestion;
    }
}

public class SuggestedRoomRequestDto
{
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int RequiredCapacity { get; set; }
    public List<string>? RequiredFacilities { get; set; }
    public Guid? PreferredBuildingId { get; set; }
    public Guid? StandardId { get; set; }
    public int? Page { get; set; }
    public int? Limit { get; set; }
}

public class SuggestedRoomDto
{
    public Guid RoomId { get; set; }
    public string RoomName { get; set; }
    public Guid BuildingId { get; set; }
    public Guid FloorId { get; set; }
    public int Capacity { get; set; }
    public List<string> Features { get; set; } = new();
    public int MatchScore { get; set; }
    public bool NeedsApproval { get; set; }
    public string ConflictStatus { get; set; } = "none";
    public List<string> Badges { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
}

public class CreateRoomReservationDto
{
    public Guid RoomId { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Purpose { get; set; }
    public RoomReservationType ReservationType { get; set; }
    public Guid? TeacherId { get; set; }
    public Guid? StandardId { get; set; }
    public Guid? DivisionId { get; set; }
    public Guid? SubjectId { get; set; }
    public string? ReservedFor { get; set; }
    public string? Remarks { get; set; }
    public bool IsRecurring { get; set; }
    public RecurringConfigDto? RecurringConfig { get; set; }
}

public class RecurringConfigDto
{
    public string RepeatType { get; set; } = "daily"; // "daily", "weekly"
    public List<int>? RepeatDays { get; set; }
    public DateTime UntilDate { get; set; }
    public bool ExcludeHolidays { get; set; } = true;
    public bool SkipConflictDates { get; set; } = false;
}

public class RoomReservationDto
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public RoomReservationType ReservationType { get; set; }
    public RoomReservationStatus Status { get; set; }
    public bool IsRecurring { get; set; }
    public Guid? RecurringConfigId { get; set; }
    public DateTime? RecurringUntilDate { get; set; }
    public string? Remarks { get; set; }
    public string? RejectionReason { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime CreationTime { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public Guid? CreatorId { get; set; }
}

public class UpdateRoomReservationStatusDto
{
    public string? ApprovalStatus { get; set; }
    public string? Status { get; set; }
    public string? Remarks { get; set; }
    public string? Reason { get; set; }
}

public class RoomMaintenanceBlockDto
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public MaintenanceSeverity Severity { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string? AssignedStaff { get; set; }
    public string? Remarks { get; set; }
    public MaintenanceStatus Status { get; set; }
}

public class CreateMaintenanceBlockDto
{
    public Guid RoomId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public MaintenanceSeverity Severity { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string? AssignedStaff { get; set; }
    public string? Remarks { get; set; }
}

public class RoomAuditLogDto
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public DateTime DateTime { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Remarks { get; set; }
    public Guid? ReservationId { get; set; }
    public Guid RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
}

public class RoomUsageReportDto
{
    public Guid RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int TotalPeriods { get; set; }
    public int OccupiedPeriods { get; set; }
    public int FreePeriods { get; set; }
    public int UtilizationPercentage { get; set; }
    public int ReservationCount { get; set; }
    public int MaintenanceCount { get; set; }
    public int ConflictCount { get; set; }
    public int CancellationCount { get; set; }
    public string MainUsageType { get; set; } = string.Empty;
}

public class RoomScheduleDto
{
    public Guid RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public List<ScheduleSlotDto> Slots { get; set; } = new();
}

public class ScheduleSlotDto
{
    public string Period { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string Status { get; set; } = "free"; // "free", "class", "exam", "event", "maintenance", "reserved"
    public string Details { get; set; } = string.Empty;
    public string? OccupiedBy { get; set; }
    public string? Teacher { get; set; }
    public Guid? ReservationId { get; set; }
}

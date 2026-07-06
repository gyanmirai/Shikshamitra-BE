namespace ShikshaMitra.Academic.Timetable;

public enum RoomReservationType
{
    Class = 1,
    Practical = 2,
    Exam = 3,
    Meeting = 4,
    Event = 5,
    Maintenance = 6,
    Other = 7
}

public enum RoomReservationStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Active = 4,
    CheckedIn = 5,
    Completed = 6,
    Cancelled = 7,
    Rejected = 8,
    NoShow = 9,
    Expired = 10
}

public enum MaintenanceSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum MaintenanceStatus
{
    Scheduled = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;

public class UnlockSeatingPlanInputDto
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Publish-time options that limit the audience, email delivery, and delivery time of the enabled school rule.
/// </summary>
public class PublishSeatingPlanInputDto
{
    public bool NotifyExamStaff { get; set; }
    public bool NotifyTeachers { get; set; }
    public bool NotifyStudents { get; set; }
    public bool NotifyParents { get; set; }
    public bool SendEmail { get; set; }
    public DateTime? ScheduleAt { get; set; }
}

public class SeatingPlanAuditLogDto
{
    public Guid Id { get; set; }
    public Guid SeatingPlanId { get; set; }
    public SeatingPlanAuditAction Action { get; set; }
    public Guid ActorUserId { get; set; }
    public string? ActorNameSnapshot { get; set; }
    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; }
}

public class SeatingPlanAuditLogResponseDto
{
    public List<SeatingPlanAuditLogDto> Items { get; set; } = [];
}

using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;

public class SeatingValidationIssueDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? StudentId { get; set; }
    public Guid? RoomId { get; set; }
    public string? SeatId { get; set; }
    public int? SeatNumber { get; set; }
    public string? RoomName { get; set; }
    public Guid? RelatedSeatingPlanId { get; set; }
    public string? RelatedSeatingPlanName { get; set; }
    public DateTime? ExamDate { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? SuggestedActionCode { get; set; }
    public string? SuggestedActionLabel { get; set; }
}

public class SeatingValidationResponseDto
{
    public bool IsValid { get; set; }
    public List<SeatingValidationIssueDto> Blockers { get; set; } = [];
    public List<SeatingValidationIssueDto> Warnings { get; set; } = [];
}

// ── Multi-room summary (spec §13.1 room summary strip / §18.3 review room table) ──

public class SeatingRoomSummaryDto
{
    public Guid RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public int Occupied { get; set; }
    public int UsableCapacity { get; set; }
    public int EmptySeats { get; set; }
    public int Utilization { get; set; }
}

public class SeatingRoomSummaryResponseDto
{
    public List<SeatingRoomSummaryDto> Rooms { get; set; } = [];
}

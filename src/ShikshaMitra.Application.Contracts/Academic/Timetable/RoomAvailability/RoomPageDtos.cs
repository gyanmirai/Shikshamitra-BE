using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public class GetRoomReservationsPageInputDto : PagedAndSortedResultRequestDto
{
    public DateTime? CreatedDate { get; set; }
    public bool IsApprovalView { get; set; }
    public Guid? RoomId { get; set; }
    public string? StatusGroup { get; set; }
    public string? SearchTerm { get; set; }
    public string[]? SearchColumns { get; set; }
    public string? Filters { get; set; }
}

public class RoomReservationPageResultDto : PagedResultDto<RoomReservationDto>
{
    public Dictionary<string, int> StatusCounts { get; set; } = new();
}

public class RoomReservationSummaryDto
{
    public Dictionary<string, int> StatusCounts { get; set; } = new();
}

public class GetRoomAuditLogsInputDto : PagedAndSortedResultRequestDto
{
    public string? SearchTerm { get; set; }
    public Guid? RoomId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Action { get; set; }
}

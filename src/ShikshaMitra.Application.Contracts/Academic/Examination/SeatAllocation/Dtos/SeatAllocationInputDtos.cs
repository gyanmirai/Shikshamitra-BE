using System;

namespace ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;

public class GetSeatLayoutInput
{
    public Guid? SeatingPlanId { get; set; }
    public Guid DivisionId { get; set; }
    public Guid RoomId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
}

public class GetSeatStudentsInput
{
    public Guid? SeatingPlanId { get; set; }
    public Guid StandardId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
}

public class GetExamDivisionsInput
{
    public Guid StandardId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
}

public class GetSeatAllocationTableInput
{
    public Guid StandardId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    /// <summary>When true, only return exams that have at least one seat allocation with IsRoomAssign = true.</summary>
    public bool? IsRoomAssigned { get; set; }
}

public class GetExamRoomsInput
{
    public Guid? SeatingPlanId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
}

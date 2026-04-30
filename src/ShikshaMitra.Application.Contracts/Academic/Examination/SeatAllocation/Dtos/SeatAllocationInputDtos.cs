using System;

namespace ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;

public class GetSeatLayoutInput
{
    public Guid DivisionId { get; set; }
    public Guid RoomId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
}

public class GetSeatStudentsInput
{
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
}

public class GetExamRoomsInput
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
}

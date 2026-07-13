using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;

public class AutoAllocateRequestDto
{
    public Guid? SeatingPlanId { get; set; }
    public List<Guid> RoomIds { get; set; } = [];

    /// <summary>roll-number | alphabetical | random | rank | alternate-division | alternate-class</summary>
    public string Strategy { get; set; } = "roll-number";

    /// <summary>front-to-back | back-to-front | row-wise | column-wise | aisle-wise</summary>
    public string FillDirection { get; set; } = "front-to-back";

    public bool LeaveGap { get; set; }
    public bool MixDivisions { get; set; }
    public bool SpecialRequirementPriority { get; set; }
    public List<Guid> ExcludedStudentIds { get; set; } = [];
}

public class AutoAllocateProposedAssignmentDto
{
    public Guid StudentId { get; set; }
    public Guid RoomId { get; set; }
    public int SeatNumber { get; set; }
    public Guid? BenchId { get; set; }
}

public class AutoAllocateResponseDto
{
    public List<AutoAllocateProposedAssignmentDto> Assignments { get; set; } = [];
    public List<Guid> UnallocatedStudentIds { get; set; } = [];
    public int TotalStudents { get; set; }
    public int RoomsUsed { get; set; }
    public int EmptySeats { get; set; }
    public int UtilizationPercent { get; set; }
}

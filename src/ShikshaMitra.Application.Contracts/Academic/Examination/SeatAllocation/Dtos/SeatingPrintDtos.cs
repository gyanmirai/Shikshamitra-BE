using System.Collections.Generic;

namespace ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;

/// <summary>Flat per-seat row shared by the room seating chart print view and the master seating list export.</summary>
public class SeatingPrintRowDto
{
    public string RoomName { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int RollNo { get; set; }
    public string StandardName { get; set; } = string.Empty;
}

public class SeatingPrintDataResponseDto
{
    public List<SeatingPrintRowDto> Rows { get; set; } = [];
}

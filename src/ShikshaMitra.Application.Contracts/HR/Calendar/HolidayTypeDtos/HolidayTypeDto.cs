using System;

namespace ShikshaMitra.HR.Calendar;

/// <summary>
/// DTO for reading HolidayType data
/// </summary>
public class HolidayTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Color { get; set; }
    public string? Description { get; set; }
}

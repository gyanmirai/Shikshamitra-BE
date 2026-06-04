using System;

namespace ShikshaMitra.HR.Calendar;

/// <summary>
/// DTO for reading HolidayType data
/// </summary>
public class HolidayTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string? Description { get; set; }
}

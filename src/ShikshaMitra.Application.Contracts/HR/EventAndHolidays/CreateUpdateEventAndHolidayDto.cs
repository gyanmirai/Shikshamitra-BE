using System;

namespace ShikshaMitra.HR.EventAndHolidays;

public class CreateUpdateEventAndHolidayDto
{
	public DateTimeOffset StartDate { get; set; }
	public DateTimeOffset EndDate { get; set; }	public string Name { get; set; }
	public string? Reason { get; set; }
	public bool	IsHoliday { get; set; }

}
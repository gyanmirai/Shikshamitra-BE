using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace ShikshaMitra.Library.Policies;

public class LibraryCalendarSettingsDto
{
	public string? TimeZoneId { get; set; }
	public string Source { get; set; } = "Unconfigured";
	public bool NeedsConfiguration { get; set; }
	public bool? HasValidOpenWeekday { get; set; }
	public bool CanPublishAutomatedPolicy { get; set; }
	public bool CanManageCalendar { get; set; }
	public List<string> ConfigurationIssues { get; set; } = new();
}

public class LibraryCalendarSettingsRequest
{
	[Required, MaxLength(100)] public string TimeZoneId { get; set; } = string.Empty;
}

public class LibraryCalendarClosureRequest
{
	public DateOnly FromDate { get; set; }
	public DateOnly ToDate { get; set; }
	public Guid? SectionId { get; set; }
	[Required, MaxLength(500)] public string Reason { get; set; } = string.Empty;
	public string? ConcurrencyStamp { get; set; }
}

public class LibraryCalendarClosureDto : LibraryCalendarClosureRequest
{
	public Guid Id { get; set; }
}

public class LibraryCalendarCalculateRequest
{
	public Guid AcademicYearId { get; set; }
	public DateOnly StartDate { get; set; }
	[Range(0, 3660)] public int Days { get; set; }
	[Range(0, 1)] public int DayCountingMode { get; set; }
	public Guid? SectionId { get; set; }
	public bool MoveToNextOpenDay { get; set; } = true;
}

public class LibraryCalendarCalculationDto : LibraryCalendarSettingsDto
{
	public DateOnly DueDate { get; set; }
}

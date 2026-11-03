using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;

public class LibraryHoursUpdateRequest
{
	[Required]
	public List<LibraryHourItemRequest> Hours { get; set; } = new();
}

public class LibraryHourItemRequest
{
	[Required]
	public string Day { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.

	[Required]
	public string StartTime { get; set; } = null!;

	[Required]
	public string EndTime { get; set; } = null!;

	[Required]
	public int Status { get; set; }
}

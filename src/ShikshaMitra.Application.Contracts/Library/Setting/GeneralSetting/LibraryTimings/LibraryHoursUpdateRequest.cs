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
	public string Day { get; set; }

	[Required]
	public string StartTime { get; set; }

	[Required]
	public string EndTime { get; set; }

	[Required]
	public int Status { get; set; }
}

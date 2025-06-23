using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings
{
	public class CreateUpdateLibraryTimingDto
	{

		[Required]
		public DayOfWeek Day { get; set; }

		[Required]
		public TimeSpan OpeningTime { get; set; }

		[Required]
		public TimeSpan ClosingTime { get; set; }
	}
}

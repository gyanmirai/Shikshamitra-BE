using System;

namespace ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings
{
	public class LibraryTimingDto
	{
		public Guid Id { get; set; }
		public DayOfWeek Day { get; set; }
		public TimeSpan OpeningTime { get; set; }
		public TimeSpan ClosingTime { get; set; }
	}
}

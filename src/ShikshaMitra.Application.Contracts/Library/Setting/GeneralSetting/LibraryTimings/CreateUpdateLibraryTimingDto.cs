using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

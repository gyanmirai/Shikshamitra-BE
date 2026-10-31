namespace ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;

public class LibraryTimingDto
{
	public string Day { get; set; } = null!; // The response mapper populates these members after construction.
	public string StartTime { get; set; } = null!;
	public string EndTime { get; set; } = null!;
	public int Status { get; set; }
}

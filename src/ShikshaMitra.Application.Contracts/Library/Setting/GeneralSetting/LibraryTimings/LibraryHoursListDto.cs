using ShikshaMitra.Library.Shared;

namespace ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;

public sealed class LibraryHoursListDto : PaginatedResponse<LibraryTimingDto>
{
	public bool CanManageHours { get; set; }
}

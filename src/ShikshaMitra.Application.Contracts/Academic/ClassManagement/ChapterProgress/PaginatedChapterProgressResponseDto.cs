using System.Collections.Generic;

namespace ShikshaMitra.Academic.ClassManagement.ChapterProgress;

public class PaginatedChapterProgressResponseDto
{
	public int TotalRecords { get; set; }
	public List<ChapterProgressRecordDto> Records { get; set; } = new();
}

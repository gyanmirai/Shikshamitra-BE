using System;

namespace ShikshaMitra.Academic.ClassManagement.ChapterProgress;

public class ChapterProgressRecordDto
{
	public Guid ChapterId { get; set; }
	public int ChapterNumber { get; set; }
	public string ChapterName { get; set; } = string.Empty;
	public DateTime? StartDate { get; set; }
	public DateTime? EndDate { get; set; }
	public int Progress { get; set; }
}

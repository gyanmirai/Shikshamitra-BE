using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Syllabus;

public class CreateUpdateChapterDto
{
	public Guid AssignedSubjectId { get; set; }
	public int ChapterNumber { get; set; }
	public string ChapterName { get; set; } = string.Empty;
	public DateTimeOffset ChapterStartTime { get; set; }
	public DateTimeOffset ChapterEndTime { get; set; }
	public string? Description { get; set; }
	public List<string> Documents { get; set; } = new List<string>();
}

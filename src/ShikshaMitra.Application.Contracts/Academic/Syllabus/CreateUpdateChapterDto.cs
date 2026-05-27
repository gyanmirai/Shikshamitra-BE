using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Syllabus;

public class CreateUpdateChapterDto
{
	public Guid AssignedSubjectId { get; set; }
	public int ChapterNo { get; set; }
	public string ChapterName { get; set; } = string.Empty;
	public DateTimeOffset StartDate { get; set; }
	public DateTimeOffset EndDate { get; set; }
	public string? Description { get; set; }
	public List<string> Documents { get; set; } = new List<string>();
}

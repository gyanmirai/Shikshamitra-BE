using System;

namespace ShikshaMitra.Academic.Syllabus;

public class CreateUpdateChapterDto
{
	public Guid StandardId { get; set; }
	public Guid SubjectId { get; set; }
	public Guid EducationMediumId { get; set; }
	public int ChapterNumber { get; set; }
	public string ChapterName { get; set; } = string.Empty;
	public DateTimeOffset ChapterStartTime { get; set; }
	public DateTimeOffset ChapterEndTime { get; set; }
	public string? Description { get; set; }
}

using System;

namespace ShikshaMitra.Academic.ClassManagement.ChapterProgress;

public class ClassDivisionSubjectDto
{
	public Guid SubjectId { get; set; }
	public string SubjectName { get; set; }
	public string SubjectCode { get; set; }
	public int TotalChapters { get; set; }
	public int CompletedChapters { get; set; }
	public int ProgressPercentage { get; set; }
}

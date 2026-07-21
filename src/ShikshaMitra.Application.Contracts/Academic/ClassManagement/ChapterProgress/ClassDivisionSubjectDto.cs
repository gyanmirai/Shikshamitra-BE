using System;

namespace ShikshaMitra.Academic.ClassManagement.ChapterProgress;

public class ClassDivisionSubjectDto
{
	public Guid SubjectId { get; set; }
	public string SubjectName { get; set; } = string.Empty;
	public string SubjectCode { get; set; } = string.Empty;
	public string SubjectColor { get; set; } = string.Empty;
	public int TotalChapters { get; set; }
	public int CompletedChapters { get; set; }
	public int ProgressPercentage { get; set; }
	public int ExpectedProgressPercentage { get; set; }
	public int VariancePercentage { get; set; }
	public string ScheduleStatus { get; set; } = string.Empty;
	public string RiskLevel { get; set; } = string.Empty;
	public DateTime? LastUpdatedAt { get; set; }
}

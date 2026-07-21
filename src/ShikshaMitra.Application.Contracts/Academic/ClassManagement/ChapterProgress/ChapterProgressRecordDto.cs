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
	public DateTime? ActualStartDate { get; set; }
	public DateTime? ActualEndDate { get; set; }
	public int ExpectedProgress { get; set; }
	public int Variance { get; set; }
	public string Status { get; set; } = string.Empty;
	public string ScheduleStatus { get; set; } = string.Empty;
	public string RiskLevel { get; set; } = string.Empty;
	public string? LastUpdatedBy { get; set; }
	public DateTime? LastUpdatedAt { get; set; }
}

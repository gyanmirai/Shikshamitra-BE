using System;

namespace ShikshaMitra.Academic.ClassManagement.ChapterProgress;

public class ChapterProgressLogDto
{
	public string ChangedByName { get; set; } = string.Empty;
	public int Progress { get; set; }
	public int? PreviousProgress { get; set; }
	public int ChangeAmount { get; set; }
	public string? Description { get; set; }
	public string? Status { get; set; }
	public string? DelayReason { get; set; }
	public DateTime? RevisedEndDate { get; set; }
	public string Source { get; set; } = "Manual Update";
	public DateTime ChangedAt { get; set; }
}

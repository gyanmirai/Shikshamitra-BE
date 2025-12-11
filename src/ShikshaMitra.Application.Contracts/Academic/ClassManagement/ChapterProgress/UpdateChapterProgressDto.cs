using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.ClassManagement.ChapterProgress;

public class UpdateChapterProgressDto
{
	[Required]
	public Guid DivisionId { get; set; }
	
	[Required]
	[Range(0, 100, ErrorMessage = "Progress must be between 0 and 100")]
	public int Progress { get; set; }
	
	[MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
	public string? Description { get; set; }
}

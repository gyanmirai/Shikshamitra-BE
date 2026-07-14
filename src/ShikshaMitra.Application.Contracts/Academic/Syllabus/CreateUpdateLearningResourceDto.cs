using System;
using ShikshaMitra.Common;

namespace ShikshaMitra.Academic.Syllabus;

public class CreateUpdateLearningResourceDto
{
	public Guid StandardId { get; set; }
	public Guid SubjectId { get; set; }
	public Guid ChapterId { get; set; }
	public Guid? DivisionId { get; set; }
	public string ResourceTitle { get; set; } = string.Empty;
	public string? Topic { get; set; }
	public string ResourceType { get; set; } = "Document";
	public string? BlobName { get; set; }
	public string? FileName { get; set; }
	public string? ContentType { get; set; }
	public long? Size { get; set; }
	public LearningResourceVisibility Visibility { get; set; } = LearningResourceVisibility.Teachers;
	public DocumentDto? LearningResource { get; set; }
}

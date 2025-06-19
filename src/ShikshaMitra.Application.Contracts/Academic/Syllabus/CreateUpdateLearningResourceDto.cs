using ShikshaMitra.Common;
using System;

namespace ShikshaMitra.Academic.Syllabus;
public class CreateUpdateLearningResourceDto
{
	public Guid StandardId { get; set; }
	public Guid SubjectId { get; set; }
	public Guid ChapterId { get; set; }
	public string ResourceTitle { get; set; }
	public DocumentDto LearningResource { get; set; }
}

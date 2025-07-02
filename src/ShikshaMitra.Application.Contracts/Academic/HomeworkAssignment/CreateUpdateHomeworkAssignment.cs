using ShikshaMitra.Common;
using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Validation;

namespace ShikshaMitra.Academic.HomeworkAssignment;
public class CreateUpdateHomeworkAssignmentDto
{
	[Required]
	public Guid StandardId { get; set; }

	[Required]
	public Guid DivisionId { get; set; }

	[Required]
	public Guid SubjectId { get; set; }

	public Guid? ChapterId { get; set; }

	[Required]
	public string Title { get; set; } = string.Empty;

	[Required]
	public string Description { get; set; } = string.Empty;

	[Required]
	public Guid HomeworkAssignmentTypeId { get; set; }

	public DocumentDto? DocumentFile { get; set; }

	[Required]
	public SubmissionType SubmissionType { get; set; }

	[Required]
	public DateTime DueDate { get; set; }
}

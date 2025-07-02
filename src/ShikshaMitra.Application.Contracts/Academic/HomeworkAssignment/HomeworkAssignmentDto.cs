using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.HomeworkAssignment;
public class HomeworkAssignmentDto : FullAuditedEntityDto<Guid>
{
	public Guid StandardId { get; set; }
	public Guid DivisionId { get; set; }
	public Guid SubjectId { get; set; }
	public Guid? ChapterId { get; set; }
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public Guid HomeworkAssignmentTypeId { get; set; }
	public string? DocumentUrl { get; set; }
	public SubmissionType SubmissionType { get; set; }
	public DateTime DueDate { get; set; }
	public bool IsPublished { get; set; }

	// Navigation properties
	public string HomeworkAssignmentTypeName { get; set; } = string.Empty;
	public string HomeworkAssignmentTypeDisplayName { get; set; } = string.Empty;
}

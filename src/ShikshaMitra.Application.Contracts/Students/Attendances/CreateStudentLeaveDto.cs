using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.Attendances;

/// <summary>
/// DTO for creating a new student leave request
/// </summary>
public class CreateStudentLeaveDto
{
	[Required]
	public Guid StudentId { get; set; }

	[Required]
	public Guid StudentLeaveTypeId { get; set; }

	[Required]
	public bool IsHalfDay { get; set; }

	[Required]
	public DateTimeOffset LeaveDate { get; set; }

	public DateTimeOffset? EndDate { get; set; }

	[MaxLength(StudentLeaveConsts.MaxDescriptionLength)]
	public string? Description { get; set; }

	public byte[]? Attachment { get; set; }

	[MaxLength(StudentLeaveConsts.MaxAttachmentFileNameLength)]
	public string? AttachmentFileName { get; set; }

	[MaxLength(StudentLeaveConsts.MaxAttachmentContentTypeLength)]
	public string? AttachmentContentType { get; set; }
}

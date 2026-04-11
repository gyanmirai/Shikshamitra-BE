using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.Attendance;

/// <summary>
/// DTO for updating an existing student leave request
/// </summary>
public class UpdateStudentLeaveDto
{
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

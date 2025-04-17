using System.ComponentModel.DataAnnotations;
using System;

namespace ShikshaMitra.Students.AttendanceAndLeaves;

public class CreateUpdateStudentLeaveDto
{
	[Required] public Guid StudentId { get; set; }

	[Required] public Guid LeaveTypeId { get; set; }

	[Required] public bool HalfDay { get; set; }

	[Required] public DateTimeOffset StartDate { get; set; }

	public DateTimeOffset EndDate { get; set; }

	[Required] public string Description { get; set; }

	// Attachment
	public byte[]? Attachment { get; set; }
}

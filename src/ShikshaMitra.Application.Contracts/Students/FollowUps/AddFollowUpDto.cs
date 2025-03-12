using ShikshaMitra.Students.StudentEnrollments;
using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.FollowUps;

public class AddFollowUpDto
{
	[Required]
	[StringLength(1000)]
	public string Message { get; set; }

	public FollowUpStatusType? InquiryStatusType { get; set; }
	public DateTimeOffset? DueDate { get; set; }
}

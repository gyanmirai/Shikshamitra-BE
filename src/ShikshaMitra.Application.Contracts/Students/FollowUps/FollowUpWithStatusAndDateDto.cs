using ShikshaMitra.Students.StudentEnrollments;
using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.FollowUps;

public class FollowUpWithStatusAndDateDto
{
	public List<FollowUpMessageDto> FollowUps { get; set; }
	public InquiryStatusType? InquiryStatusType { get; set; }
	public DateTimeOffset? DueDate { get; set; }
}

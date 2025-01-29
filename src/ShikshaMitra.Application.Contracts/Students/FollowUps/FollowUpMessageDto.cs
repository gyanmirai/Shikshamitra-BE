using ShikshaMitra.Students.StudentEnrollments;
using System;

namespace ShikshaMitra.Students.FollowUps;

public class FollowUpMessageDto
{
	public string Message { get; set; }
	public string Author { get; set; }
	public DateTimeOffset Timestamp { get; set; }
}

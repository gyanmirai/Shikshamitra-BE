using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentRecords;

public class AssignStudentToGroupDto
{
	[Required]
	public Guid StudentId { get; set; }

	[Required]
	public Guid StudentGroupId { get; set; }

	public bool IsLeader { get; set; }
}

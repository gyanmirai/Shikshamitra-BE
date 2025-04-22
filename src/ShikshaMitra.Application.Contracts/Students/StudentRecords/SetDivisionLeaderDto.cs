using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentRecords;

public class SetDivisionLeaderDto
{
	[Required]
	public Guid StudentId { get; set; }

	[Required]
	public Guid StudentGroupId { get; set; }
}
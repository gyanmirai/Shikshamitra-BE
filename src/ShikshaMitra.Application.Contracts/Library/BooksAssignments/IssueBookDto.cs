using System;

namespace ShikshaMitra.Library.BooksAssignments;

public class IssueBookDto
{
	public Guid BookId { get; set; }
	public Guid MemberId { get; set; }
	public DateTime IssueDate { get; set; }
	public DateTime DueDate { get; set; }
}

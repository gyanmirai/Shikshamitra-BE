using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Library.BooksAssignments;

public class CreateUpdateBooksAssignmentDto
{
	public Guid BookId { get; set; }
	public Guid MemberId { get; set; }
    public string BookTitle { get; set; }
	public DateTime IssueDate { get; set; }
	public DateTime DueDate { get; set; }
    public DateTime? ReturnDate { get; set; }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Library.BooksAssignments;
public class ReturnBookDto
{
	public Guid BookId { get; set; }
	public Guid MemberId { get; set; }
	public DateTime ReturnDate { get; set; }
}

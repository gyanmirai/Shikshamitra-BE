using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Library.Books;

public class BookOverviewDto
{
	public Guid Id { get; set; }
	public string Isbn { get; set; }
	public string Name { get; set; }
	public int BorrowedCount { get; set; }
	public int OverdueCount { get; set; }
	public int ReservedCount { get; set; }
}

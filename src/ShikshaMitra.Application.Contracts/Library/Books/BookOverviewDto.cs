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
	public int Borrowed { get; set; }
	public int Overdue { get; set; }
	public int Reserved { get; set; }
}

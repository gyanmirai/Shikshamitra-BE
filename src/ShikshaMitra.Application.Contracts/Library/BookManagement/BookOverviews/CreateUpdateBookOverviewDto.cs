using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Library.BookManagement.BookOverviews;

public class CreateUpdateBookOverviewDto
{
	public string ISBN { get; set; }
	public string Name { get; set; }
	public int Borrowed { get; set; }
	public int Overdue { get; set; }
	public int Reserved { get; set; }
}

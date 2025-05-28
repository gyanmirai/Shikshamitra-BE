using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Library.BookManagement.AddNewBook;

public class CreateUpdateAddNewBookDto
{
	public string BookTitle { get; set; }
	public string BookAuthor { get; set; }
	public string ISBN { get; set; }
	public string Publisher { get; set; }
	public int Year { get; set; }
	public int AvailableCopies { get; set; }
	public string Category { get; set; }
	public string Condition { get; set; }
	public string PicturePath { get; set; }
	public string EditionNotes { get; set; }
}

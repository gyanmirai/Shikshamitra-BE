using System;
namespace ShikshaMitra.Library.Books;

public class AddNewBookDto
{
	public string BookTitle { get; set; }
	public string BookAuthor { get; set; }
	public string ISBN { get; set; }
	public string Publisher { get; set; }
	public int Year { get; set; }
	public int AvailableCopies { get; set; }
	public Guid BookCategoryId { get; set; }
	public string PicturePath { get; set; }
	public string EditionNotes { get; set; }
}

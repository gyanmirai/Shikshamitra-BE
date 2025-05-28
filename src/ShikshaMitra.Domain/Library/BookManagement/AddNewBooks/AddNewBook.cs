using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.BookManagement.AddNewBooks;
public class AddNewBook : AuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }

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

	private AddNewBook() { }

	public AddNewBook(
		Guid id,
		string bookTitle,
		string bookAuthor,
		string isbn,
		string publisher,
		int year,
		int availableCopies,
		string category,
		string condition,
		string picturePath,
		string editionNotes,
		Guid? tenantId)
		: base(id)
	{
		BookTitle = bookTitle;
		BookAuthor = bookAuthor;
		ISBN = isbn;
		Publisher = publisher;
		Year = year;
		AvailableCopies = availableCopies;
		Category = category;
		Condition = condition;
		PicturePath = picturePath;
		EditionNotes = editionNotes;
		TenantId = tenantId;
	}
}
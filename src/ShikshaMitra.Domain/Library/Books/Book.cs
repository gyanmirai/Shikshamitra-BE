using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Books;
public class Book : AuditedAggregateRoot<Guid>, IMultiTenant
{
		public Guid? TenantId { get; set; }

		public string BookTitle { get; set; }
		public string BookAuthor { get; set; }
		public string ISBN { get; set; }
		public string Publisher { get; set; }
		public int Year { get; set; }
		public int AvailableCopies { get; set; }
		public Guid BookCategoryId { get; set; }
		public string PicturePath { get; set; }
		public string EditionNotes { get; set; }

	private Book() { }

	public Book(
		Guid id,
		string bookTitle,
		string bookAuthor,
		string isbn,
		string publisher,
		int year,
		int availableCopies,
		Guid bookCategoryId,
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
		BookCategoryId = bookCategoryId;
		PicturePath = picturePath;
		EditionNotes = editionNotes;
		TenantId = tenantId;
	}
}

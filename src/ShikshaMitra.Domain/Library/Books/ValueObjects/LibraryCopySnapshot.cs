using System;

namespace ShikshaMitra.Library.Books;

public sealed record LibraryCopySnapshot(Guid CopyId, Guid BookId, Guid? TenantId, string AccessionNumber,
	BookCopyStatus Status, Guid SectionId, Guid ShelfId, string? Remarks)
{
	public static LibraryCopySnapshot Capture(BookCopy copy) =>
		new(copy.Id, copy.BookId, copy.TenantId, copy.AccessionNumber, copy.Status, copy.SectionId, copy.ShelfId, copy.Remarks);
}

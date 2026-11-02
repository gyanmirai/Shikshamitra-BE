using System;

namespace ShikshaMitra.Library.Books;

public class BookCopyDto
{
    public Guid Id { get; set; }
    public int CopyNumber { get; set; }
    public string AccessionNumber { get; set; } = null!; // The response mapper populates these members after construction.
    public Guid SectionId { get; set; }
    public Guid ShelfId { get; set; }
    public string Status { get; set; } = null!;
    public string? Remarks { get; set; }
}

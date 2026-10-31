using System;
using ShikshaMitra.Library.Shared;

namespace ShikshaMitra.Library.Books;

public class BookDetailCopyDto
{
    public Guid CopyId { get; set; }
    public string AccessionNo { get; set; } = null!; // The response mapper populates these members after construction.
    public int Status { get; set; }
    public NamedIdDto Section { get; set; } = null!;
    public NamedIdDto Shelf { get; set; } = null!;
    public string? Remarks { get; set; }
}

using System;
using ShikshaMitra.Library.Shared;

namespace ShikshaMitra.Library.Books;

public class BookDetailCopyDto
{
    public Guid CopyId { get; set; }
    public string AccessionNo { get; set; }
    public int Status { get; set; }
    public NamedIdDto Section { get; set; }
    public NamedIdDto Shelf { get; set; }
    public string? Remarks { get; set; }
}

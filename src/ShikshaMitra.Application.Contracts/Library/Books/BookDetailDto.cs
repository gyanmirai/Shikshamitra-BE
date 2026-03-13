using System;
using System.Collections.Generic;
using ShikshaMitra.Library.Shared;

namespace ShikshaMitra.Library.Books;

public class BookDetailDto
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string Author { get; set; }
    public string Publisher { get; set; }
    public string Subject { get; set; }
    public string Isbn { get; set; }
    public int Language { get; set; }
    public string Edition { get; set; }
    public NamedIdDto Category { get; set; }
    public string ApplicableAge { get; set; }
    public int Pages { get; set; }
    public decimal Price { get; set; }
    public string? CoverImage { get; set; }
    public DateTime AddedOn { get; set; }
    public List<BookDetailCopyDto> Copies { get; set; } = new();
}

using System;

namespace ShikshaMitra.Library.Books;

public class BookDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!; // The response mapper populates these members after construction.
    public string Isbn { get; set; } = null!;
    public string Author { get; set; } = null!;
    public string Publisher { get; set; } = null!;
    public string Edition { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public Guid CategoryId { get; set; }
    public int Language { get; set; }
    public string ApplicableAge { get; set; } = null!;
    public decimal Price { get; set; }
    public int Pages { get; set; }
    public int NumberOfCopies { get; set; }
    public string? CoverImage { get; set; }
}

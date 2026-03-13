using System;

namespace ShikshaMitra.Library.Books;

public class BookDto
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string Isbn { get; set; }
    public string Author { get; set; }
    public string Publisher { get; set; }
    public string Edition { get; set; }
    public string Subject { get; set; }
    public Guid CategoryId { get; set; }
    public int Language { get; set; }
    public string ApplicableAge { get; set; }
    public decimal Price { get; set; }
    public int Pages { get; set; }
    public int NumberOfCopies { get; set; }
    public string? CoverImage { get; set; }
}

using System;
using System.Collections.Generic;
using ShikshaMitra.Library.Shared;

namespace ShikshaMitra.Library.Books;

public class BookDetailDto
{
	public string MetadataRevision { get; set; } = null!;
	public string CopiesRevision { get; set; } = null!;
	public bool CanDelete { get; set; }
	public string? DeletionBlockedReason { get; set; }
    public Guid Id { get; set; }
    public string Title { get; set; } = null!; // The response mapper populates these members after construction.
    public string Author { get; set; } = null!;
    public string Publisher { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public string Isbn { get; set; } = null!;
	public int? Language { get; set; }
	public string? LanguageCode { get; set; }
	public string? LanguageLabel { get; set; }
	public string? MaterialCode { get; set; }
	public string? MaterialLabel { get; set; }
    public string Edition { get; set; } = null!;
    public NamedIdDto Category { get; set; } = null!;
    public string ApplicableAge { get; set; } = null!;
    public int Pages { get; set; }
    public decimal? Price { get; set; }
	public string? PriceText { get; set; }
    public string? CoverImage { get; set; }
    public DateTime AddedOn { get; set; }
    public List<BookDetailCopyDto> Copies { get; set; } = new();
}

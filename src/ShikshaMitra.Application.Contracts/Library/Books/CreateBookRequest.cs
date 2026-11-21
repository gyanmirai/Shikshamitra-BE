using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Library.Books;

public class CreateBookRequest
{
	public string? ExpectedCatalogueConfigurationRevision { get; set; }
	public string? LanguageCode { get; set; }
	public string? MaterialCode { get; set; }
    [Required]
    public string Title { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.

    public string? Isbn { get; set; }

	public string? Author { get; set; }

    public string Publisher { get; set; } = null!;

    public string Edition { get; set; } = null!;

    public string Subject { get; set; } = null!;

    [Required]
    public Guid CategoryId { get; set; }

	public Language? Language { get; set; }

    public string ApplicableAge { get; set; } = null!;

    public decimal? Price { get; set; }
	public string? PriceText { get; set; }

    public int Pages { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int NumberOfCopies { get; set; }

    public string? CoverImage { get; set; }

    public List<BookCopyItem> Copies { get; set; } = new();
}

public class UpdateBookRequest
{
	public string? ExpectedMetadataRevision { get; set; }
	public string? ExpectedCopiesRevision { get; set; }
	public string? ExpectedCatalogueConfigurationRevision { get; set; }
	public string? LanguageCode { get; set; }
	public string? MaterialCode { get; set; }
    [Required]
    public string Title { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.

    public string? Isbn { get; set; }

	public string? Author { get; set; }

    public string Publisher { get; set; } = null!;

    public string Edition { get; set; } = null!;

    public string Subject { get; set; } = null!;

    [Required]
    public Guid CategoryId { get; set; }

	public Language? Language { get; set; }

    public string ApplicableAge { get; set; } = null!;

    public decimal? Price { get; set; }
	public string? PriceText { get; set; }

    public int Pages { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int NumberOfCopies { get; set; }

    public string? CoverImage { get; set; }

    public List<BookCopyItem> Copies { get; set; } = new();
}

using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Library.Books;

public class CreateBookRequest
{
    [Required]
    public string Title { get; set; }

    [Required]
    public string Isbn { get; set; }

    [Required]
    public string Author { get; set; }

    public string Publisher { get; set; }

    public string Edition { get; set; }

    public string Subject { get; set; }

    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    public Language Language { get; set; }

    public string ApplicableAge { get; set; }

    public decimal Price { get; set; }

    public int Pages { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Number of copies must be at least 1.")]
    public int NumberOfCopies { get; set; }

    public string? CoverImage { get; set; }
}

public class UpdateBookRequest
{
    [Required]
    public string Title { get; set; }

    [Required]
    public string Isbn { get; set; }

    [Required]
    public string Author { get; set; }

    public string Publisher { get; set; }

    public string Edition { get; set; }

    public string Subject { get; set; }

    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    public Language Language { get; set; }

    public string ApplicableAge { get; set; }

    public decimal Price { get; set; }

    public int Pages { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Number of copies must be at least 1.")]
    public int NumberOfCopies { get; set; }

    public string? CoverImage { get; set; }
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Library.Books;

public class PutBookCopiesRequest
{
    [Required]
    public List<BookCopyItem> Copies { get; set; } = new();
}

public class BookCopyItem
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Copy number must be at least 1.")]
    public int CopyNumber { get; set; }

    [Required]
    public string AccessionNumber { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.

    [Required]
    public Guid SectionId { get; set; }

    [Required]
    public Guid ShelfId { get; set; }
}

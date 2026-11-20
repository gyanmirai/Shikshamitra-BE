using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Library.Books;

public class PutBookCopiesRequest
{
	public string? ExpectedCopiesRevision { get; set; }
    [Required]
    public List<BookCopyItem> Copies { get; set; } = new();
}

public class BookCopyItem
{
    public Guid? CopyId { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Copy number must be at least 1.")]
    public int CopyNumber { get; set; }

    [MaxLength(50)]
    public string? AccessionNumber { get; set; }

    [Required]
    public Guid SectionId { get; set; }

    [Required]
    public Guid ShelfId { get; set; }
}

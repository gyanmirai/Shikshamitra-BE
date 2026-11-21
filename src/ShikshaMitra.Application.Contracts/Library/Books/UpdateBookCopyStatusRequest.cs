using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Library.Books;

public class UpdateBookCopyStatusRequest
{
    [Required]
    public BookCopyStatus Status { get; set; }

    public string? Remarks { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }

    [StringLength(128)]
    public string? Revision { get; set; }
}

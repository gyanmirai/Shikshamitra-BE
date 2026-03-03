using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Library.Books;

public class UpdateBookCopyStatusRequest
{
    [Required]
    public BookCopyStatus Status { get; set; }

    public string? Remarks { get; set; }
}

using System;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Library.Books;

namespace ShikshaMitra.Library.IssueReturn;

public class ReturnBookRequest
{
    [Required]
    public string AccessionNo { get; set; }

    [Required]
    public Guid MemberId { get; set; }

    [Required]
    public DateTime ReturnDate { get; set; }

    public Guid? ChargeCategoryId { get; set; }

    public decimal? Amount { get; set; }

    public DateTime? FineDueDate { get; set; }

    public BookCopyStatus? CopyStatus { get; set; }

    public string? Remarks { get; set; }
}

using System;
using ShikshaMitra.Library.Books;

namespace ShikshaMitra.Library.IssueReturn;

public class ReturnBookRequest
{
    public Guid? LoanId { get; set; }
    public LibraryLoanReturnRequest? Return { get; set; }

    public string? AccessionNo { get; set; }

    public Guid MemberId { get; set; }

    public DateTime ReturnDate { get; set; }

    public Guid? ChargeCategoryId { get; set; }

    public decimal? Amount { get; set; }

    public DateTime? FineDueDate { get; set; }

    public BookCopyStatus? CopyStatus { get; set; }

    public string? Remarks { get; set; }
}

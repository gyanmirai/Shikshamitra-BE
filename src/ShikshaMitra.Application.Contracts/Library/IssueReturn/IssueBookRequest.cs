using System;

namespace ShikshaMitra.Library.IssueReturn;

public class IssueBookRequest
{
    public LibraryCheckoutRequest? Checkout { get; set; }

    public string? AccessionNo { get; set; }

    public Guid MemberId { get; set; }

    public DateTime IssueDate { get; set; }

    public DateTime DueDate { get; set; }
}

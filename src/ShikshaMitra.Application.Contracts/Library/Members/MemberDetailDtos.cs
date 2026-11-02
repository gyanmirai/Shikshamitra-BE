using System;

namespace ShikshaMitra.Library.Members;

public class MemberBorrowedBookDto
{
    public Guid Id { get; set; }
    public string? BookTitle { get; set; }
    public string? BookCategory { get; set; }
    public string AccessionNo { get; set; } = null!;
    public DateTime IssuedOn { get; set; }
    public DateTime DueOn { get; set; }
}

public class MemberReservedBookDto
{
    public Guid Id { get; set; }
    public string? BookTitle { get; set; }
    public string? BookCategory { get; set; }
    public string? Author { get; set; }
    public DateTime ReservedOn { get; set; }
}

public class MemberChargeItemDto
{
    public Guid Id { get; set; }
    public string? BookTitle { get; set; }
    public string? BookCategory { get; set; }
    public string? AccessionNo { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string ChargeType { get; set; } = null!; // The response mapper populates these members after construction.
    public string? Reason { get; set; }
    public int PaymentStatus { get; set; }
}

public class MemberBorrowHistoryDto
{
    public Guid BorrowId { get; set; }
    public Guid BookId { get; set; }
    public string? BookTitle { get; set; }
    public string? BookCategory { get; set; }
    public string AccessionNo { get; set; } = null!;
    public DateTime IssuedOn { get; set; }
    public DateTime DueOn { get; set; }
}

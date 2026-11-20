using System;
using ShikshaMitra.Library.Members.Dtos;
using ShikshaMitra.Library.Shared;

namespace ShikshaMitra.Library.Members;

public class MemberBorrowedBookDto
{
    public Guid Id { get; set; }
    public Guid? BookId { get; set; }
    public Guid? CopyId { get; set; }
    public string CopyLinkSource { get; set; } = "Unavailable";
    public bool RequiresCopyReconciliation { get; set; }
    public Guid? AcademicYearId { get; set; }
    public DateOnly? SchoolIssueDate { get; set; }
    public DateOnly? SchoolDueDate { get; set; }
    public int IssueDateProvenance { get; set; }
    public int DueDateProvenance { get; set; }
    public string? BookTitle { get; set; }
    public string? BookCategory { get; set; }
    public string AccessionNo { get; set; } = null!;
    public DateTime? IssuedOn { get; set; }
    public DateTime? DueOn { get; set; }
}

public class MemberReservedBookDto
{
    public Guid Id { get; set; }
    public Guid? AcademicYearId { get; set; }
    public DateOnly? RequestedDate { get; set; }
    public DateOnly? RequestExpiryDate { get; set; }
    public int RequestedDateProvenance { get; set; }
    public int RequestExpiryDateProvenance { get; set; }
    public int? State { get; set; }
    public string? BookTitle { get; set; }
    public string? BookCategory { get; set; }
    public string? Author { get; set; }
    public DateTime? ReservedOn { get; set; }
}

public class MemberChargeItemDto
{
    public Guid Id { get; set; }
    public Guid? BookId { get; set; }
    public Guid? CopyId { get; set; }
    public string CopyLinkSource { get; set; } = "Unavailable";
    public bool RequiresCopyReconciliation { get; set; }
    public string? BookTitle { get; set; }
    public string? BookCategory { get; set; }
    public string? AccessionNo { get; set; }
    public decimal Amount { get; set; }
    public string? AmountText { get; set; }
    public string? RecordedCurrency { get; set; }
    public Guid? AcademicYearId { get; set; }
    public LibraryAcademicYearProvenance AcademicYearProvenance { get; set; }
    public DateTime? Date { get; set; }
    public DateOnly? SchoolChargeDate { get; set; }
    public DateOnly? SchoolDueDate { get; set; }
    public int ChargeDateProvenance { get; set; }
    public int DueDateProvenance { get; set; }
    public string ChargeType { get; set; } = null!; // The response mapper populates these members after construction.
    public string? Reason { get; set; }
    public int PaymentStatus { get; set; }
    public LibraryMemberChargePositionDto? FinancePosition { get; set; }
}

public class MemberBorrowHistoryDto
{
    public Guid BorrowId { get; set; }
    public Guid BookId { get; set; }
    public Guid? CopyId { get; set; }
    public string CopyLinkSource { get; set; } = "Unavailable";
    public bool RequiresCopyReconciliation { get; set; }
    public Guid? AcademicYearId { get; set; }
    public DateOnly? SchoolIssueDate { get; set; }
    public DateOnly? SchoolDueDate { get; set; }
    public DateOnly? SchoolReturnDate { get; set; }
    public int IssueDateProvenance { get; set; }
    public int DueDateProvenance { get; set; }
    public int ReturnDateProvenance { get; set; }
    public DateTime? ReturnedOn { get; set; }
    public int? StoredStatus { get; set; }
    public string? BookTitle { get; set; }
    public string? BookCategory { get; set; }
    public string AccessionNo { get; set; } = null!;
    public DateTime? IssuedOn { get; set; }
    public DateTime? DueOn { get; set; }
}

using System;
using ShikshaMitra.Library.Shared;

namespace ShikshaMitra.Library.Charges;

public class ChargeListItemDto
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public string? MemberName { get; set; }
    public int Role { get; set; }
    public string ChargeType { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime? ChargeDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateOnly? SchoolChargeDate { get; set; }
    public DateOnly? SchoolDueDate { get; set; }
    public LibraryCivilDateProvenance ChargeDateProvenance { get; set; }
    public LibraryCivilDateProvenance DueDateProvenance { get; set; }
    public Guid? AcademicYearId { get; set; }
    public LibraryAcademicYearProvenance AcademicYearProvenance { get; set; }
    public string? Currency { get; set; }
    public Guid? FinanceSourceLinkId { get; set; }
    public string SourceVersion { get; set; } = string.Empty;
    public int PaymentStatus { get; set; }
    public DateTime? PaidDate { get; set; }
    public string? BookTitle { get; set; }
    public string? AccessionNo { get; set; }
}

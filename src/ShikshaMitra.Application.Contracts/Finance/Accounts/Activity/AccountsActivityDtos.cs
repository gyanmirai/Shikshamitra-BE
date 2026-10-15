using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Accounts.Activity;

public sealed class AccountsActivityInput
{
    public Guid AcademicYearId { get; set; }
    public string? Direction { get; set; }
    public int? Month { get; set; }
    public int? Year { get; set; }
    public Guid? CategoryId { get; set; }
    public string? Source { get; set; }
    public string? Search { get; set; }
    public string? Filters { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class AccountsActivityRowDto
{
    public Guid Id { get; set; }
    public Guid SourceId { get; set; }
    public Guid? StudentId { get; set; }
    public Guid? FinanceFeeAccountId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? GrNumber { get; set; }
    public string ClassDivision { get; set; } = string.Empty;
    public string? Counterparty { get; set; }
    public Guid? CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public Guid? BankAccountId { get; set; }
    public string BankLabel { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string SourceStatus { get; set; } = string.Empty;
    public string AccountingStatus { get; set; } = "Not posted";
    public decimal Amount { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public bool CanOpenSource { get; set; }
}

public sealed class AccountsActivityPageDto
{
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<AccountsActivityRowDto> Items { get; set; } = [];
    public List<AccountsCategoryOptionDto> Categories { get; set; } = [];
}

public sealed record AccountsCategoryOptionDto(Guid Id, string Label);

public sealed class AccountsActivityDetailDto : AccountsActivityRowDto
{
    public Guid AcademicYearId { get; set; }
    public string AcademicYearName { get; set; } = string.Empty;
    public string? ApprovedPaymentMethod { get; set; }
    public string? ClassName { get; set; }
    public string? DivisionName { get; set; }
    public string? Notes { get; set; }
    public string? OriginalReceiptNumber { get; set; }
    public string? RefundReason { get; set; }
    public string? ApprovalNotes { get; set; }
    public string? RefundStatus { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? FailureReason { get; set; }
    public List<AccountsAllocationDto> Allocations { get; set; } = [];
    public List<AccountsPaymentDto> Payments { get; set; } = [];
    public List<AccountsCategoryAmountDto> CategoryAmounts { get; set; } = [];
}
public sealed record AccountsAllocationDto(string Name, string? Period, decimal Amount,
    DateTime? CoverageStartDate = null, DateTime? CoverageEndDate = null);
public sealed record AccountsPaymentDto(string Method, decimal Amount, DateTimeOffset Date,
    string? BankLabel, string? Reference, string? EvidenceReference = null, string? Status = null);
public sealed record AccountsCategoryAmountDto(string Category, decimal Amount, string? BankLabel);

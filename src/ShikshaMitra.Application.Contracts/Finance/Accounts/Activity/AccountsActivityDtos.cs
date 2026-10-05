using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Accounts.Activity;

public sealed class AccountsActivityInput
{
    public Guid AcademicYearId { get; set; }
    public string? Source { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed class AccountsActivityRowDto
{
    public Guid Id { get; set; }
    public Guid SourceId { get; set; }
    public Guid? StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
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
}

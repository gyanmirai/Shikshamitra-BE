using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Accounts.Settings.Dtos;

public class BankAccountResponse
{
	public Guid Id { get; set; }
	public Guid AcademicYearId { get; set; }
	public string? Code { get; set; }
	public Guid? AccountHeadId { get; set; }
	public string? AccountHeadName { get; set; }
	public string BankName { get; set; } = string.Empty;
	public string BranchName { get; set; } = string.Empty;
	public string? BranchAddress { get; set; }
	public string AccountHolderName { get; set; } = string.Empty;
	public BankAccountType AccountType { get; set; }
	public string? AccountNumber { get; set; }
	public string MaskedAccountNumber { get; set; } = string.Empty;
	public string? Purpose { get; set; }
	public string IfscCode { get; set; } = string.Empty;
	public string? UpiId { get; set; }
	public string? MaskedUpiId { get; set; }
	public DateTimeOffset? EffectiveFrom { get; set; }
	public DateTimeOffset? EffectiveTo { get; set; }
	public bool IsActive { get; set; }
	public string? DeactivationReason { get; set; }
	public int Version { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public bool IsUsedInDownstream { get; set; }
	public bool CanDelete => !IsActive && !IsUsedInDownstream;
}

public class PaginatedBankAccountResponse
{
	public long TotalCount { get; set; }
	public int CurrentPage { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public List<BankAccountResponse> Items { get; set; } = [];
}

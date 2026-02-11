using System;
using System.Collections.Generic;
using ShikshaMitra.Finance.Setting.BankAccount;

namespace ShikshaMitra.Application.Contracts.Finance.Setting.BankAccount.Dtos;

public class BankAccountResponse
{
    public Guid Id { get; set; }
    public string BankName { get; set; }
    public string BranchName { get; set; }
    public string? BranchAddress { get; set; }
    public string AccountHolderName { get; set; }
    public BankAccountType AccountType { get; set; }
    public string AccountNumber { get; set; }
    public string? Purpose { get; set; }
    public string IfscCode { get; set; }
    public string? UpiId { get; set; }
    public bool IsActive { get; set; }
}

public class PaginatedBankAccountResponse
{
    public long TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<BankAccountResponse> Items { get; set; }
}

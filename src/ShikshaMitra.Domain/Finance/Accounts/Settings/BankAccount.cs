using ShikshaMitra.Finance.Setting;
using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Accounts.Settings;

public class BankAccount : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; set; }
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

    protected BankAccount() { }

    public BankAccount(
        Guid id,
        Guid academicYearId,
        string bankName,
        string branchName,
        string? branchAddress,
        string accountHolderName,
        BankAccountType accountType,
        string accountNumber,
        string? purpose,
        string ifscCode,
        string? upiId,
        bool isActive = true)
        : base(id)
    {
        AcademicYearId = academicYearId;
        BankName = bankName;
        BranchName = branchName;
        BranchAddress = branchAddress;
        AccountHolderName = accountHolderName;
        AccountType = accountType;
        AccountNumber = accountNumber;
        Purpose = purpose;
        IfscCode = ifscCode;
        UpiId = upiId;
        IsActive = isActive;
    }
}

using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Finance.Setting;

namespace ShikshaMitra.Finance.Accounts.Settings.BankAccount.Dtos;

public class CreateBankAccountRequest
{
    [Required]
    public string BankName { get; set; }

    [Required]
    public string BranchName { get; set; }

    public string? BranchAddress { get; set; }

    [Required]
    public string AccountHolderName { get; set; }

    public BankAccountType AccountType { get; set; }

    [Required]
    public string AccountNumber { get; set; }

    public string? Purpose { get; set; }

    [Required]
    public string IfscCode { get; set; }

    public string? UpiId { get; set; }
}

public class UpdateBankAccountRequest
{
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

using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Accounts.Settings.Dtos;

public class CreateBankAccountRequest
{
	// Optional for legacy clients. New bank accounts receive a server-generated internal code.
	public string? Code { get; set; }
	[System.Text.Json.Serialization.JsonIgnore]
	public Guid? AccountHeadId { get; set; }
	[Required]
	public string BankName { get; set; } = string.Empty;
	[Required]
	public string BranchName { get; set; } = string.Empty;
	public string? BranchAddress { get; set; }
	[Required]
	public string AccountHolderName { get; set; } = string.Empty;
	public BankAccountType AccountType { get; set; }
	[Required]
	public string AccountNumber { get; set; } = string.Empty;
	public string? Purpose { get; set; }
	[Required]
	public string IfscCode { get; set; } = string.Empty;
	public string? UpiId { get; set; }
	public DateTimeOffset? EffectiveFrom { get; set; }
	public DateTimeOffset? EffectiveTo { get; set; }
	public string? ConcurrencyStamp { get; set; }
}

public class UpdateBankAccountRequest : CreateBankAccountRequest;

using System;

namespace ShikshaMitra.Students.Records.Profiles;

/// <summary>
/// DTO for updating bank details of a student in Registration status
/// </summary>
public class UpdateBankDetailsDto
{
	/// <summary>
	/// Name of the bank
	/// </summary>
	public string? BankName { get; set; }

	/// <summary>
	/// Bank account number
	/// </summary>
	public string? AccountNo { get; set; }

	/// <summary>
	/// IFSC code of the bank branch
	/// </summary>
	public string? IFSCCode { get; set; }

	/// <summary>
	/// Bank branch name
	/// </summary>
	public string? BranchName { get; set; }

	/// <summary>
	/// Account holder name
	/// </summary>
	public string? AccountHolderName { get; set; }

	/// <summary>
	/// Type of account (Savings, Current, etc.)
	/// </summary>
	public string? AccountType { get; set; }

	/// <summary>
	/// Bank address
	/// </summary>
	public string? BankAddress { get; set; }

	/// <summary>
	/// MICR code
	/// </summary>
	public string? MICRCode { get; set; }

	/// <summary>
	/// Swift code for international transfers
	/// </summary>
	public string? SwiftCode { get; set; }
}

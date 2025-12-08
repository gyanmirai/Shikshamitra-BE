using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students;

/// <summary>
/// Entity representing bank details of a student (1:1 relationship with Student aggregate)
/// Stores banking information for fee payments and refunds
/// </summary>
public class StudentBankDetail : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	/// <summary>
	/// Name of the bank
	/// </summary>
	public string? BankName { get; private set; }

	/// <summary>
	/// Bank account number
	/// </summary>
	public string? AccountNo { get; private set; }

	/// <summary>
	/// IFSC code of the bank branch
	/// </summary>
	public string? IFSCCode { get; private set; }

	/// <summary>
	/// Bank branch name
	/// </summary>
	public string? BranchName { get; private set; }

	/// <summary>
	/// Account holder name
	/// </summary>
	public string? AccountHolderName { get; private set; }

	/// <summary>
	/// Type of account (Savings, Current, etc.)
	/// </summary>
	public string? AccountType { get; private set; }

	/// <summary>
	/// Bank address
	/// </summary>
	public string? BankAddress { get; private set; }

	/// <summary>
	/// MICR code
	/// </summary>
	public string? MICRCode { get; private set; }

	/// <summary>
	/// Swift code for international transfers
	/// </summary>
	public string? SwiftCode { get; private set; }

	private StudentBankDetail()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	public StudentBankDetail(
		Guid id,
		Guid? tenantId)
		: base(id)
	{
		TenantId = tenantId;
	}

	/// <summary>
	/// Updates all bank details information at once
	/// </summary>
	public void UpdateBankDetails(
		string? bankName,
		string? accountNo,
		string? ifscCode,
		string? branchName,
		string? accountHolderName,
		string? accountType,
		string? bankAddress,
		string? micrCode,
		string? swiftCode)
	{
		BankName = bankName;
		AccountNo = accountNo;
		IFSCCode = ifscCode;
		BranchName = branchName;
		AccountHolderName = accountHolderName;
		AccountType = accountType;
		BankAddress = bankAddress;
		MICRCode = micrCode;
		SwiftCode = swiftCode;
	}
}

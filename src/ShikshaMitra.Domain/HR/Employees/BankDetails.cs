using System.Collections.Generic;
using Volo.Abp.Domain.Values;

namespace ShikshaMitra.HR.Employees;

public class BankDetails : ValueObject
{
	public string? BankName { get; private set; }
	public string? BranchName { get; private set; }
	public string? AccountHolderName { get; private set; }
	public string? AccountNumber { get; private set; }
	public string? IfscCode { get; private set; }

	public BankDetails(string? bankName, string? branchName, string? accountHolderName, string? accountNumber,
		string? ifscCode)
	{
		BankName = bankName;
		BranchName = branchName;
		AccountHolderName = accountHolderName;
		AccountNumber = accountNumber;
		IfscCode = ifscCode;
	}

	protected override IEnumerable<object> GetAtomicValues()
	{
		yield return BankName;
		yield return BranchName;
		yield return AccountHolderName;
		yield return AccountNumber;
		yield return IfscCode;
	}
}
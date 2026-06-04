using System.Collections.Generic;
using Volo.Abp.Domain.Values;

namespace ShikshaMitra.HR.Employees;

public class BankDetails(
	string? bankName,
	string? branchName,
	string? accountHolderName,
	string? accountNumber,
	string? ifscCode)
	: ValueObject
{
	public string? BankName { get; private set; } = bankName;
	public string? BranchName { get; private set; } = branchName;
	public string? AccountHolderName { get; private set; } = accountHolderName;
	public string? AccountNumber { get; private set; } = accountNumber;
	public string? IfscCode { get; private set; } = ifscCode;

	protected override IEnumerable<object> GetAtomicValues()
	{
		yield return BankName ?? string.Empty;
		yield return BranchName ?? string.Empty;
		yield return AccountHolderName ?? string.Empty;
		yield return AccountNumber ?? string.Empty;
		yield return IfscCode ?? string.Empty;
	}
}

using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using System;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	private static readonly SeedBankBranch[] SeedBankBranches =
	[
		new("State Bank of India", "Surat City Light", "SBIN0005842"),
		new("HDFC Bank", "Surat Ghod Dod Road", "HDFC0000267"),
		new("ICICI Bank", "Surat Adajan", "ICIC0000412"),
		new("Bank of Baroda", "Surat Ring Road", "BARB0SURRIN"),
		new("Axis Bank", "Surat Vesu", "UTIB0000631")
	];

	internal static BankDetails BuildSeedEmployeeBankDetails(SuratSeedEmployeeProfile profile)
	{
		var data = BuildSeedEmployeeFinancialData(profile);
		return new BankDetails(
			data.BankName,
			data.BranchName,
			data.AccountHolderName,
			data.AccountNumber,
			data.IfscCode);
	}

	internal static SeedEmployeeFinancialData BuildSeedEmployeeFinancialData(
		SuratSeedEmployeeProfile profile,
		string? employeeCode = null) =>
		BuildSeedEmployeeFinancialData(
			profile.Email,
			$"{profile.FirstName} {profile.LastName}",
			profile.EmployeeGroup,
			employeeCode);

	private static SeedEmployeeFinancialData BuildSeedEmployeeFinancialData(Employee employee) =>
		BuildSeedEmployeeFinancialData(
			employee.Email ?? employee.Id.ToString("N"),
			employee.EmployeeName,
			employee.EmployeeGroup,
			employee.EmployeeCode);

	private static SeedEmployeeFinancialData BuildSeedEmployeeFinancialData(
		string seedKey,
		string accountHolderName,
		EmployeeGroupType? employeeGroup,
		string? employeeCode)
	{
		var seed = StableSeedHash(seedKey);
		var branch = SeedBankBranches[(int)(seed % SeedBankBranches.Length)];
		var accountNumber = $"40{seed:D10}";
		var uanNumber = $"10{seed:D10}";
		var epfNumber = $"GJ/SRT/{seed % 1_000_000:D6}/{StableSeedHash($"{seedKey}:epf") % 1_000:D3}";
		var punchId = !string.IsNullOrWhiteSpace(employeeCode)
			? $"DDVS-{employeeCode}"
			: $"DDVS-{seed % 100_000:D5}";
		var workingHours = employeeGroup == EmployeeGroupType.Teaching ? 176m : 168m;

		return new SeedEmployeeFinancialData(
			branch.BankName,
			branch.BranchName,
			accountHolderName,
			accountNumber,
			branch.IfscCode,
			epfNumber,
			uanNumber,
			punchId,
			workingHours);
	}

	private static bool ShouldRepairSeedPayrollDetails(
		SeedEmployeeFinancialData data,
		string? pfRuleId,
		string? bankName,
		string? branchName,
		string? accountHolderName,
		string? accountNumber,
		string? ifscCode,
		string? pfRule,
		string? epfNumber,
		string? uanNumber,
		string? punchId,
		decimal? totalWorkingHourPerMonth) =>
		IsBlank(bankName) ||
		IsBlank(branchName) ||
		IsBlank(accountHolderName) ||
		IsBlank(accountNumber) ||
		IsBlank(ifscCode) ||
		IsBlank(epfNumber) ||
		IsBlank(uanNumber) ||
		IsBlank(punchId) ||
		totalWorkingHourPerMonth is null or <= 0m ||
		string.Equals(accountHolderName, "Demo Account Holder", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(accountNumber, "12345678", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(ifscCode, "SBIN0001234", StringComparison.OrdinalIgnoreCase) ||
		(!string.IsNullOrWhiteSpace(pfRuleId) && !string.Equals(pfRule, pfRuleId, StringComparison.OrdinalIgnoreCase)) ||
		!string.Equals(bankName, data.BankName, StringComparison.OrdinalIgnoreCase) ||
		!string.Equals(branchName, data.BranchName, StringComparison.OrdinalIgnoreCase);

	private static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);

	internal sealed record SeedEmployeeFinancialData(
		string BankName,
		string BranchName,
		string AccountHolderName,
		string AccountNumber,
		string IfscCode,
		string EpfNumber,
		string UanNumber,
		string PunchId,
		decimal TotalWorkingHourPerMonth);

	private sealed record SeedBankBranch(string BankName, string BranchName, string IfscCode);
}

using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.HR.Employees.Payroll;

/// <summary>
/// Represents detailed payroll configuration for an employee.
/// </summary>
public class EmployeePayrollDetails : Entity<Guid>
{
	public Guid EmployeeId { get; set; }

	public string? BankName { get; set; }

	public string? BranchName { get; set; }

	public string? AccountHolderName { get; set; }

	public string? AccountNumber { get; set; }

	public string? IfscCode { get; set; }

	public string? PfRule { get; set; }

	public string? EpfNumber { get; set; }

	public string? UanNumber { get; set; }

	public string? PunchId { get; set; }

	public decimal? BasicSalary { get; set; }

	public decimal? TdsPercentage { get; set; }

	public decimal? TdsAmount { get; set; }

	// Navigation property
	public Employee Employee { get; set; } = null!;

	protected EmployeePayrollDetails()
	{
	}

	public EmployeePayrollDetails(Guid id, Guid employeeId) : base(id)
	{
		EmployeeId = employeeId;
	}

	public void UpdateDetails(
		string? bankName,
		string? branchName,
		string? accountHolderName,
		string? accountNumber,
		string? ifscCode,
		string? pfRule,
		string? epfNumber,
		string? uanNumber,
		string? punchId,
		decimal? basicSalary,
		decimal? tdsPercentage,
		decimal? tdsAmount)
	{
		BankName = bankName;
		BranchName = branchName;
		AccountHolderName = accountHolderName;
		AccountNumber = accountNumber;
		IfscCode = ifscCode;
		PfRule = pfRule;
		EpfNumber = epfNumber;
		UanNumber = uanNumber;
		PunchId = punchId;
		BasicSalary = basicSalary;
		TdsPercentage = tdsPercentage;
		TdsAmount = tdsAmount;
	}
}

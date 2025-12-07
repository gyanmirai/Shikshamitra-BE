using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.HR.Employees.Payroll;

/// <summary>
/// Represents a single payroll component (earning or deduction) for an employee.
/// </summary>
public class EmployeePayrollComponent : Entity<Guid>
{
	public Guid EmployeeId { get; set; }
	
	public string ComponentName { get; set; } = string.Empty;
	
	public PayrollComponentType Type { get; set; }
	
	public decimal Amount { get; set; }
	
	public bool IsEarning { get; set; }

	// Navigation property
	public Employee Employee { get; set; } = null!;

	protected EmployeePayrollComponent()
	{
	}

	public EmployeePayrollComponent(
		Guid id,
		Guid employeeId,
		string componentName,
		PayrollComponentType type,
		decimal amount,
		bool isEarning) : base(id)
	{
		EmployeeId = employeeId;
		ComponentName = componentName;
		Type = type;
		Amount = amount;
		IsEarning = isEarning;
	}

	public void UpdateComponent(PayrollComponentType type, decimal amount)
	{
		Type = type;
		Amount = amount;
	}
}


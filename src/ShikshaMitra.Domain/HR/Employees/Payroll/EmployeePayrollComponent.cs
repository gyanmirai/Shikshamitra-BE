using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.HR.Employees.Payroll;

/// <summary>
/// Represents a single payroll component (earning or deduction) for an employee.
/// </summary>
public class EmployeePayrollComponent : Entity<Guid>
{
	public Guid EmployeeId { get; set; }
	
	/// <summary>
	/// Component name (e.g., Basic, PF, Tax).
	/// </summary>
	public string Name { get; set; } = string.Empty;
	
	/// <summary>
	/// Optional description/explanation of the component.
	/// </summary>
	public string? Description { get; set; }
	
	/// <summary>
	/// Final calculated amount.
	/// </summary>
	public decimal Amount { get; set; }
	
	/// <summary>
	/// Whether this is a fixed component for all employees or employee-specific.
	/// </summary>
	public bool IsFixed { get; set; }
	
	/// <summary>
	/// Whether this is a temporary or one-time component.
	/// </summary>
	public bool IsTemp { get; set; }
	
	/// <summary>
	/// Calculation strategy (Fixed amount or Percentage-based).
	/// </summary>
	public SalaryComponentType ComponentType { get; set; }
	
	/// <summary>
	/// What this component depends on for calculation (Basic or Gross).
	/// Only applicable when ComponentType is Percentage.
	/// </summary>
	public SalaryComponentDependency? DependOn { get; set; }
	
	/// <summary>
	/// True for earning components, false for deduction components.
	/// </summary>
	public bool IsEarning { get; set; }

	// Navigation property
	public Employee Employee { get; set; } = null!;

	protected EmployeePayrollComponent()
	{
	}

	public EmployeePayrollComponent(
		Guid id,
		Guid employeeId,
		string name,
		string? description,
		decimal amount,
		bool isFixed,
		bool isTemp,
		SalaryComponentType componentType,
		SalaryComponentDependency? dependOn,
		bool isEarning) : base(id)
	{
		EmployeeId = employeeId;
		Name = name;
		Description = description;
		Amount = amount;
		IsFixed = isFixed;
		IsTemp = isTemp;
		ComponentType = componentType;
		DependOn = dependOn;
		IsEarning = isEarning;
	}

	public void UpdateComponent(
		string name,
		string? description,
		decimal amount,
		bool isFixed,
		bool isTemp,
		SalaryComponentType componentType,
		SalaryComponentDependency? dependOn)
	{
		Name = name;
		Description = description;
		Amount = amount;
		IsFixed = isFixed;
		IsTemp = isTemp;
		ComponentType = componentType;
		DependOn = dependOn;
	}
}


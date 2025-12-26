using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

/// <summary>
/// Response DTO for salary slip as per OpenAPI contract
/// </summary>
public class SalarySlipResponse
{
    /// <summary>
    /// Employee identifier
    /// </summary>
    [Required]
    public Guid EmployeeId { get; set; }

    /// <summary>
    /// Salary period in MMMM-YYYY format (e.g., JANUARY-2025)
    /// </summary>
    [Required]
    [RegularExpression(@"^[A-Z]+-\d{4}$", ErrorMessage = "MonthYear must be in MMMM-YYYY format (e.g., JANUARY-2025)")]
    public string MonthYear { get; set; } = string.Empty;

    /// <summary>
    /// Computed total earnings
    /// </summary>
    [Required]
    [Range(0, double.MaxValue)]
    public double TotalEarning { get; set; }

    /// <summary>
    /// Computed total deductions
    /// </summary>
    [Required]
    [Range(0, double.MaxValue)]
    public double TotalDeduction { get; set; }

    /// <summary>
    /// Net payable amount (totalEarning - totalDeduction)
    /// </summary>
    [Required]
    [Range(0, double.MaxValue)]
    public double NetPay { get; set; }

    /// <summary>
    /// Salary earning components
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one earning component is required")]
    public List<SalaryComponent> Earnings { get; set; } = new();

    /// <summary>
    /// Salary deduction components
    /// </summary>
    [Required]
    public List<SalaryComponent> Deductions { get; set; } = new();

    /// <summary>
    /// Salary slip generation timestamp (ISO-8601 with timezone)
    /// </summary>
    [Required]
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Salary component DTO as per OpenAPI contract
/// </summary>
public class SalaryComponent
{
    /// <summary>
    /// Component name (e.g., Basic, PF, Tax)
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional explanation
    /// </summary>
    [StringLength(255)]
    public string? Description { get; set; }

    /// <summary>
    /// Final calculated amount
    /// </summary>
    [Required]
    [Range(0, double.MaxValue)]
    public double Amount { get; set; }

    /// <summary>
    /// Fixed for all employees or employee-specific
    /// </summary>
    [Required]
    public bool IsFixed { get; set; }

    /// <summary>
    /// Temporary or one-time component
    /// </summary>
    [Required]
    public bool IsTemp { get; set; }

    /// <summary>
    /// Calculation strategy (FIXED or PERCENTAGE)
    /// </summary>
    [Required]
    public string ComponentType { get; set; } = string.Empty;

    /// <summary>
    /// Required only when componentType is PERCENTAGE (BASIC or GROSS)
    /// </summary>
    public string? DependOn { get; set; }
}
